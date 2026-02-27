// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Source: https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Collections/src/System/Collections/Generic/PriorityQueue.cs
// Adapted for Unity / .NET Standard 2.1 compatibility:
//   - Replaced ArgumentNullException.ThrowIfNull / ArgumentOutOfRangeException.ThrowIfNegative
//     with manual null / range checks.
//   - Replaced Array.MaxLength constant (introduced in .NET 6) with its numeric value.
//   - Replaced internal EnumerableHelpers with self-contained helpers.
//   - Removed DebuggerTypeProxy attribute (references internal PriorityQueueDebugView).

#if !NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ProceduralVegetation.Core
{
    /// <summary>
    ///  Represents a min priority queue.
    /// </summary>
    /// <typeparam name="TElement">Specifies the type of elements in the queue.</typeparam>
    /// <typeparam name="TPriority">Specifies the type of priority associated with enqueued elements.</typeparam>
    /// <remarks>
    ///  Implements an array-backed quaternary min-heap. Each element is enqueued with an
    ///  associated priority that determines the dequeue order: elements with the lowest
    ///  priority get dequeued first.
    /// </remarks>
    [DebuggerDisplay("Count = {Count}")]
    public class PriorityQueue<TElement, TPriority>
    {
        /// <summary>Represents an implicit heap-ordered complete d-ary tree, stored as an array.</summary>
        private (TElement Element, TPriority Priority)[] _nodes;

        /// <summary>Custom comparer used to order the heap.</summary>
        private readonly IComparer<TPriority>? _comparer;

        /// <summary>Lazily-initialized collection used to expose the contents of the queue.</summary>
        private UnorderedItemsCollection? _unorderedItems;

        /// <summary>The number of nodes in the heap.</summary>
        private int _size;

        /// <summary>Version updated on mutation to help validate enumerators operate on a consistent state.</summary>
        private int _version;

        /// <summary>Specifies the arity of the d-ary heap (quaternary).</summary>
        private const int Arity = 4;

        /// <summary>The binary logarithm of <see cref="Arity"/>.</summary>
        private const int Log2Arity = 2;

        // Array.MaxLength was introduced in .NET 6; the backing value is 0x7FFFFFC7.
        private const int ArrayMaxLength = 0x7FFFFFC7;

        // ------------------------------------------------------------------ //
        //  Constructors                                                        //
        // ------------------------------------------------------------------ //

        /// <summary>Initializes a new instance of the <see cref="PriorityQueue{TElement, TPriority}"/> class.</summary>
        public PriorityQueue()
        {
            _nodes = Array.Empty<(TElement, TPriority)>();
            _comparer = InitializeComparer(null);
        }

        /// <summary>Initializes a new instance with the specified initial capacity.</summary>
        public PriorityQueue(int initialCapacity)
            : this(initialCapacity, comparer: null) { }

        /// <summary>Initializes a new instance with the specified custom priority comparer.</summary>
        public PriorityQueue(IComparer<TPriority>? comparer)
        {
            _nodes = Array.Empty<(TElement, TPriority)>();
            _comparer = InitializeComparer(comparer);
        }

        /// <summary>Initializes a new instance with the specified initial capacity and custom priority comparer.</summary>
        public PriorityQueue(int initialCapacity, IComparer<TPriority>? comparer)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Non-negative number required.");

            _nodes = new (TElement, TPriority)[initialCapacity];
            _comparer = InitializeComparer(comparer);
        }

        /// <summary>Initializes a new instance populated with the specified elements and priorities.</summary>
        public PriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items)
            : this(items, comparer: null) { }

        /// <summary>Initializes a new instance populated with the specified elements/priorities and custom comparer.</summary>
        public PriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items, IComparer<TPriority>? comparer)
        {
            if (items is null)
                throw new ArgumentNullException(nameof(items));

            _nodes = ToArray(items, out _size);
            _comparer = InitializeComparer(comparer);

            if (_size > 1)
                Heapify();
        }

        // ------------------------------------------------------------------ //
        //  Public properties                                                   //
        // ------------------------------------------------------------------ //

        /// <summary>Gets the number of elements contained in the queue.</summary>
        public int Count => _size;

        /// <summary>Gets the priority comparer used by the queue.</summary>
        public IComparer<TPriority> Comparer => _comparer ?? Comparer<TPriority>.Default;

        /// <summary>Gets a collection that enumerates the elements of the queue in an unordered manner.</summary>
        public UnorderedItemsCollection UnorderedItems => _unorderedItems ??= new UnorderedItemsCollection(this);

        // ------------------------------------------------------------------ //
        //  Core operations                                                     //
        // ------------------------------------------------------------------ //

        /// <summary>Adds the specified element with associated priority to the queue.</summary>
        public void Enqueue(TElement element, TPriority priority)
        {
            int currentSize = _size;
            _version++;

            if (_nodes.Length == currentSize)
                Grow(currentSize + 1);

            _size = currentSize + 1;

            if (_comparer == null)
                MoveUpDefaultComparer((element, priority), currentSize);
            else
                MoveUpCustomComparer((element, priority), currentSize);
        }

        /// <summary>Returns the minimal element without removing it.</summary>
        /// <exception cref="InvalidOperationException">The queue is empty.</exception>
        public TElement Peek()
        {
            if (_size == 0)
                throw new InvalidOperationException("Queue is empty.");

            return _nodes[0].Element;
        }

        /// <summary>Removes and returns the minimal element.</summary>
        /// <exception cref="InvalidOperationException">The queue is empty.</exception>
        public TElement Dequeue()
        {
            if (_size == 0)
                throw new InvalidOperationException("Queue is empty.");

            TElement element = _nodes[0].Element;
            RemoveRootNode();
            return element;
        }

        /// <summary>
        ///  Removes the minimal element and then immediately adds the specified element with associated priority.
        /// </summary>
        /// <exception cref="InvalidOperationException">The queue is empty.</exception>
        public TElement DequeueEnqueue(TElement element, TPriority priority)
        {
            if (_size == 0)
                throw new InvalidOperationException("Queue is empty.");

            (TElement Element, TPriority Priority) root = _nodes[0];

            if (_comparer == null)
            {
                if (Comparer<TPriority>.Default.Compare(priority, root.Priority) > 0)
                    MoveDownDefaultComparer((element, priority), 0);
                else
                    _nodes[0] = (element, priority);
            }
            else
            {
                if (_comparer.Compare(priority, root.Priority) > 0)
                    MoveDownCustomComparer((element, priority), 0);
                else
                    _nodes[0] = (element, priority);
            }

            _version++;
            return root.Element;
        }

        /// <summary>
        ///  Tries to remove the minimal element and copy it to <paramref name="element"/> and its priority to
        ///  <paramref name="priority"/>.
        /// </summary>
        public bool TryDequeue([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
        {
            if (_size != 0)
            {
                (element, priority) = _nodes[0];
                RemoveRootNode();
                return true;
            }

            element = default;
            priority = default;
            return false;
        }

        /// <summary>
        ///  Tries to return the minimal element without removing it.
        /// </summary>
        public bool TryPeek([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
        {
            if (_size != 0)
            {
                (element, priority) = _nodes[0];
                return true;
            }

            element = default;
            priority = default;
            return false;
        }

        /// <summary>
        ///  Adds the specified element with associated priority, then immediately removes and returns the minimal element.
        /// </summary>
        public TElement EnqueueDequeue(TElement element, TPriority priority)
        {
            if (_size != 0)
            {
                (TElement Element, TPriority Priority) root = _nodes[0];

                if (_comparer == null)
                {
                    if (Comparer<TPriority>.Default.Compare(priority, root.Priority) > 0)
                    {
                        MoveDownDefaultComparer((element, priority), 0);
                        _version++;
                        return root.Element;
                    }
                }
                else
                {
                    if (_comparer.Compare(priority, root.Priority) > 0)
                    {
                        MoveDownCustomComparer((element, priority), 0);
                        _version++;
                        return root.Element;
                    }
                }
            }

            return element;
        }

        /// <summary>Enqueues a sequence of element/priority pairs.</summary>
        public void EnqueueRange(IEnumerable<(TElement Element, TPriority Priority)> items)
        {
            if (items is null)
                throw new ArgumentNullException(nameof(items));

            int count = 0;
            var collection = items as ICollection<(TElement Element, TPriority Priority)>;
            if (collection is not null && (count = collection.Count) > _nodes.Length - _size)
                Grow(checked(_size + count));

            if (_size == 0)
            {
                if (collection is not null)
                {
                    collection.CopyTo(_nodes, 0);
                    _size = count;
                }
                else
                {
                    int i = 0;
                    (TElement, TPriority)[] nodes = _nodes;
                    foreach ((TElement element, TPriority priority) in items)
                    {
                        if (nodes.Length == i)
                        {
                            Grow(i + 1);
                            nodes = _nodes;
                        }
                        nodes[i++] = (element, priority);
                    }
                    _size = i;
                }

                _version++;

                if (_size > 1)
                    Heapify();
            }
            else
            {
                foreach ((TElement element, TPriority priority) in items)
                    Enqueue(element, priority);
            }
        }

        /// <summary>Enqueues a sequence of elements, all associated with the specified priority.</summary>
        public void EnqueueRange(IEnumerable<TElement> elements, TPriority priority)
        {
            if (elements is null)
                throw new ArgumentNullException(nameof(elements));

            if (elements is ICollection<TElement> collection && collection.Count > _nodes.Length - _size)
                Grow(checked(_size + collection.Count));

            if (_size == 0)
            {
                int i = 0;
                (TElement, TPriority)[] nodes = _nodes;
                foreach (TElement element in elements)
                {
                    if (nodes.Length == i)
                    {
                        Grow(i + 1);
                        nodes = _nodes;
                    }
                    nodes[i++] = (element, priority);
                }
                _size = i;
                _version++;

                if (i > 1)
                    Heapify();
            }
            else
            {
                foreach (TElement element in elements)
                    Enqueue(element, priority);
            }
        }

        /// <summary>Removes all items from the queue.</summary>
        public void Clear()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<(TElement, TPriority)>())
                Array.Clear(_nodes, 0, _size);

            _size = 0;
            _version++;
        }

        /// <summary>Ensures the queue can hold up to <paramref name="capacity"/> items without further expansion.</summary>
        public int EnsureCapacity(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Non-negative number required.");

            if (_nodes.Length < capacity)
            {
                Grow(capacity);
                _version++;
            }

            return _nodes.Length;
        }

        /// <summary>
        ///  Sets the capacity to the actual number of items if that is less than 90% of current capacity.
        /// </summary>
        public void TrimExcess()
        {
            int threshold = (int)(_nodes.Length * 0.9);
            if (_size < threshold)
            {
                Array.Resize(ref _nodes, _size);
                _version++;
            }
        }

        // ------------------------------------------------------------------ //
        //  Private helpers                                                     //
        // ------------------------------------------------------------------ //

        private void Grow(int minCapacity)
        {
            const int GrowFactor = 2;
            const int MinimumGrow = 4;

            int newCapacity = GrowFactor * _nodes.Length;

            if ((uint)newCapacity > ArrayMaxLength)
                newCapacity = ArrayMaxLength;

            newCapacity = Math.Max(newCapacity, _nodes.Length + MinimumGrow);

            if (newCapacity < minCapacity)
                newCapacity = minCapacity;

            Array.Resize(ref _nodes, newCapacity);
        }

        private void RemoveRootNode()
        {
            int lastNodeIndex = --_size;
            _version++;

            if (lastNodeIndex > 0)
            {
                (TElement Element, TPriority Priority) lastNode = _nodes[lastNodeIndex];

                if (_comparer == null)
                    MoveDownDefaultComparer(lastNode, 0);
                else
                    MoveDownCustomComparer(lastNode, 0);
            }

            if (RuntimeHelpers.IsReferenceOrContainsReferences<(TElement, TPriority)>())
                _nodes[lastNodeIndex] = default;
        }

        private static int GetParentIndex(int index) => (index - 1) >> Log2Arity;
        private static int GetFirstChildIndex(int index) => (index << Log2Arity) + 1;

        private void Heapify()
        {
            (TElement Element, TPriority Priority)[] nodes = _nodes;
            int lastParentWithChildren = GetParentIndex(_size - 1);

            if (_comparer == null)
            {
                for (int index = lastParentWithChildren; index >= 0; --index)
                    MoveDownDefaultComparer(nodes[index], index);
            }
            else
            {
                for (int index = lastParentWithChildren; index >= 0; --index)
                    MoveDownCustomComparer(nodes[index], index);
            }
        }

        private void MoveUpDefaultComparer((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            Debug.Assert(_comparer is null);
            Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

            (TElement Element, TPriority Priority)[] nodes = _nodes;

            while (nodeIndex > 0)
            {
                int parentIndex = GetParentIndex(nodeIndex);
                (TElement Element, TPriority Priority) parent = nodes[parentIndex];

                if (Comparer<TPriority>.Default.Compare(node.Priority, parent.Priority) < 0)
                {
                    nodes[nodeIndex] = parent;
                    nodeIndex = parentIndex;
                }
                else
                {
                    break;
                }
            }

            nodes[nodeIndex] = node;
        }

        private void MoveUpCustomComparer((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            Debug.Assert(_comparer is not null);
            Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

            IComparer<TPriority> comparer = _comparer;
            (TElement Element, TPriority Priority)[] nodes = _nodes;

            while (nodeIndex > 0)
            {
                int parentIndex = GetParentIndex(nodeIndex);
                (TElement Element, TPriority Priority) parent = nodes[parentIndex];

                if (comparer.Compare(node.Priority, parent.Priority) < 0)
                {
                    nodes[nodeIndex] = parent;
                    nodeIndex = parentIndex;
                }
                else
                {
                    break;
                }
            }

            nodes[nodeIndex] = node;
        }

        private void MoveDownDefaultComparer((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            Debug.Assert(_comparer is null);
            Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

            (TElement Element, TPriority Priority)[] nodes = _nodes;
            int size = _size;

            int i;
            while ((i = GetFirstChildIndex(nodeIndex)) < size)
            {
                (TElement Element, TPriority Priority) minChild = nodes[i];
                int minChildIndex = i;

                int childIndexUpperBound = Math.Min(i + Arity, size);
                while (++i < childIndexUpperBound)
                {
                    (TElement Element, TPriority Priority) nextChild = nodes[i];
                    if (Comparer<TPriority>.Default.Compare(nextChild.Priority, minChild.Priority) < 0)
                    {
                        minChild = nextChild;
                        minChildIndex = i;
                    }
                }

                if (Comparer<TPriority>.Default.Compare(node.Priority, minChild.Priority) <= 0)
                    break;

                nodes[nodeIndex] = minChild;
                nodeIndex = minChildIndex;
            }

            nodes[nodeIndex] = node;
        }

        private void MoveDownCustomComparer((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            Debug.Assert(_comparer is not null);
            Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

            IComparer<TPriority> comparer = _comparer;
            (TElement Element, TPriority Priority)[] nodes = _nodes;
            int size = _size;

            int i;
            while ((i = GetFirstChildIndex(nodeIndex)) < size)
            {
                (TElement Element, TPriority Priority) minChild = nodes[i];
                int minChildIndex = i;

                int childIndexUpperBound = Math.Min(i + Arity, size);
                while (++i < childIndexUpperBound)
                {
                    (TElement Element, TPriority Priority) nextChild = nodes[i];
                    if (comparer.Compare(nextChild.Priority, minChild.Priority) < 0)
                    {
                        minChild = nextChild;
                        minChildIndex = i;
                    }
                }

                if (comparer.Compare(node.Priority, minChild.Priority) <= 0)
                    break;

                nodes[nodeIndex] = minChild;
                nodeIndex = minChildIndex;
            }

            nodes[nodeIndex] = node;
        }

        private static IComparer<TPriority>? InitializeComparer(IComparer<TPriority>? comparer)
        {
            if (typeof(TPriority).IsValueType)
            {
                if (comparer == Comparer<TPriority>.Default)
                    return null;

                return comparer;
            }
            else
            {
                return comparer ?? Comparer<TPriority>.Default;
            }
        }

        /// <summary>Copies an enumerable to a new array, returning the number of items written.</summary>
        private static (TElement, TPriority)[] ToArray(
            IEnumerable<(TElement Element, TPriority Priority)> source, out int length)
        {
            if (source is ICollection<(TElement, TPriority)> col)
            {
                int count = col.Count;
                if (count == 0)
                {
                    length = 0;
                    return Array.Empty<(TElement, TPriority)>();
                }

                var arr = new (TElement, TPriority)[count];
                col.CopyTo(arr, 0);
                length = count;
                return arr;
            }

            var list = new List<(TElement, TPriority)>(source);
            length = list.Count;
            return list.ToArray();
        }

        // ------------------------------------------------------------------ //
        //  UnorderedItemsCollection                                            //
        // ------------------------------------------------------------------ //

        /// <summary>
        ///  Enumerates the contents of a <see cref="PriorityQueue{TElement, TPriority}"/>, without any ordering guarantees.
        /// </summary>
        [DebuggerDisplay("Count = {Count}")]
        public sealed class UnorderedItemsCollection
            : IReadOnlyCollection<(TElement Element, TPriority Priority)>, ICollection
        {
            internal readonly PriorityQueue<TElement, TPriority> _queue;

            internal UnorderedItemsCollection(PriorityQueue<TElement, TPriority> queue) => _queue = queue;

            public int Count => _queue._size;
            object ICollection.SyncRoot => this;
            bool ICollection.IsSynchronized => false;

            void ICollection.CopyTo(Array array, int index)
            {
                if (array is null)
                    throw new ArgumentNullException(nameof(array));
                if (array.Rank != 1)
                    throw new ArgumentException("Multi-dimensional arrays are not supported.", nameof(array));
                if (array.GetLowerBound(0) != 0)
                    throw new ArgumentException("Non-zero lower bound arrays are not supported.", nameof(array));
                if (index < 0 || index > array.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));
                if (array.Length - index < _queue._size)
                    throw new ArgumentException("Insufficient array length.");

                try
                {
                    Array.Copy(_queue._nodes, 0, array, index, _queue._size);
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException("Incompatible array type.", nameof(array));
                }
            }

            /// <summary>Enumerates the elements of an <see cref="UnorderedItemsCollection"/>.</summary>
            public struct Enumerator : IEnumerator<(TElement Element, TPriority Priority)>
            {
                private readonly PriorityQueue<TElement, TPriority> _queue;
                private readonly int _version;
                private int _index;
                private (TElement, TPriority) _current;

                internal Enumerator(PriorityQueue<TElement, TPriority> queue)
                {
                    _queue = queue;
                    _index = 0;
                    _version = queue._version;
                    _current = default;
                }

                public void Dispose() { }

                public bool MoveNext()
                {
                    PriorityQueue<TElement, TPriority> localQueue = _queue;

                    if (_version == localQueue._version && (uint)_index < (uint)localQueue._size)
                    {
                        _current = localQueue._nodes[_index];
                        _index++;
                        return true;
                    }

                    return MoveNextRare();
                }

                private bool MoveNextRare()
                {
                    if (_version != _queue._version)
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

                    _index = _queue._size + 1;
                    _current = default;
                    return false;
                }

                public (TElement Element, TPriority Priority) Current => _current;
                object IEnumerator.Current => _current;

                void IEnumerator.Reset()
                {
                    if (_version != _queue._version)
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

                    _index = 0;
                    _current = default;
                }
            }

            public Enumerator GetEnumerator() => new Enumerator(_queue);

            IEnumerator<(TElement Element, TPriority Priority)>
                IEnumerable<(TElement Element, TPriority Priority)>.GetEnumerator() =>
                    _queue.Count == 0
                        ? (IEnumerator<(TElement Element, TPriority Priority)>)
                          Array.Empty<(TElement Element, TPriority Priority)>().GetEnumerator()
                        : GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                ((IEnumerable<(TElement Element, TPriority Priority)>)this).GetEnumerator();
        }
    }
}

#endif
