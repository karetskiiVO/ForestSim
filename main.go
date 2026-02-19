package main

import (
	"fmt"
	"os"
	"strings"
)

type VecInfo struct {
	VecType   string
	VecCoords []string
}

func main() {
	vecInfos := []VecInfo{
		{VecType: "Vector2", VecCoords: []string{"x", "y"}},
		{VecType: "Vector3", VecCoords: []string{"x", "y", "z"}},
		{VecType: "Vector4", VecCoords: []string{"x", "y", "z", "w"}},
	}

	writer, _ := os.Create("Assets/Plugins/karetskiiVO/ProceduralVegetation/Assets/Scripts/Utilities.gen.cs")
	defer writer.Close()

	fmt.Fprintln(writer, "namespace ProceduralVegetation.Utilities {")
	fmt.Fprintln(writer, "\tpublic static class GeneratedVectorExtents {")

	for _, vecInfo := range vecInfos {
		minDims := 2
		for dims := minDims; dims <= 4; dims++ {
			multiIter := NewMultiIter(len(vecInfo.VecCoords), dims)
			for {
				coords := multiIter.Apply(vecInfo.VecCoords)

				name := strings.Join(coords, "")

				params := make([]string, len(coords))
				copy(params, coords)
				for i := range params {
					params[i] = "val." + params[i]
				}

				fmt.Fprintln(writer, "\t\t[MethodImpl(MethodImplOptions.AggressiveInlining)]")
				fmt.Fprintf(writer, "\t\tpublic static Vector%v %s (this Vector%d val) => new Vector%v(%s);\n",
					dims,
					name,
					len(vecInfo.VecCoords),
					dims,
					strings.Join(params, ", "),
				)

				if !multiIter.Next() {
					break
				}
			}
			fmt.Fprintln(writer)
		}
		fmt.Fprintln(writer)
	}

	fmt.Fprintln(writer, "\t}\n}")
}

type MultiIter struct {
	indices []int // Исправлено: ideces -> indices
	n       int
}

func NewMultiIter(in, out int) *MultiIter {
	indices := make([]int, out)
	// Инициализируем индексы нулями
	for i := range indices {
		indices[i] = 0
	}
	return &MultiIter{
		indices: indices,
		n:       in,
	}
}

func (m *MultiIter) Next() bool {
	var next func(pos int) bool
	next = func(pos int) bool {
		if pos == len(m.indices) {
			return false
		}

		if m.indices[pos] < m.n-1 {
			m.indices[pos]++
			return true
		}

		m.indices[pos] = 0
		return next(pos + 1)
	}

	return next(0)
}

func (m *MultiIter) Apply(val []string) []string {
	res := make([]string, len(m.indices)) // Исправлено: размер должен быть len(m.indices)

	for i := range m.indices {
		res[i] = val[m.indices[i]]
	}

	return res
}
