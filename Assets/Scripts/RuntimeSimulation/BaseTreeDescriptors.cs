using System.Linq;

using ProceduralVegetation.Utilities;

using Sirenix.Utilities;

using UnityEngine;

using static ProceduralVegetation.Simulation;

namespace ProceduralVegetation {
    public abstract class TreeSpeciesCountDescriptor : TreeSpeciesDescriptor {
        public void ResetCount() { count = 0; }

        public void HandleInstance(ref FoliageInstance instance) {
            if (instance.type == FoliageInstance.FoliageType.Dying) return;

            count++;
        }

        int count = 0;
        public int Count => count;

        public class TreeSpeciesCounterEventGenerator : EventGenerator {
            public override ProceduralVegetation.Simulation.Event[] Generate(float currentTime) {
                return new ProceduralVegetation.Simulation.Event[] {
                    new TreeSpeciesCounterEvent(){ time = 0.999f },
                };
            }

            public class TreeSpeciesCounterEvent : ProceduralVegetation.Simulation.Event {
                public override void Execute(ref SimulationContext context) {
                    for (int i = 0; i < context.points.Count; i++) {
                        var descriptor = context.speciesDescriptors[context.points[i].speciesID];

                        if (descriptor is not TreeSpeciesCountDescriptor) continue;
                        var point = context.points[i];
                        (descriptor as TreeSpeciesCountDescriptor).ResetCount();
                    }

                    for (int i = 0; i < context.points.Count; i++) {
                        var descriptor = context.speciesDescriptors[context.points[i].speciesID];

                        if (descriptor is not TreeSpeciesCountDescriptor) continue;
                        var point = context.points[i];
                        (descriptor as TreeSpeciesCountDescriptor).HandleInstance(ref point.foliageInstance);
                        context.points[i] = point;
                    }
                }
            }
        }
    }

    public class RuntimeSpeciesDescriptor : TreeSpeciesCountDescriptor {
        protected float energyStressWeight = 1f;
        protected float requiredEnergy = 1f;
        protected float waterStressWeight = 1f;
        protected float requiredWater = 1f;
        // protected float lightStressWeight = 1f;
        // protected float requiredLight = 1f;

        protected float acceptableSeedStress = 0.5f;
        protected float acceptableSaplingStress = 0.7f;
        protected float acceptableMatureStress = 0.9f;

        protected float seedEnergy = 1f;

        protected float saplingStartAge = 1f;
        protected float matureStartAge = 5f;

        protected float seedSpreadRadius = 100f;
        protected int seedSpreadCount = 5;

        protected float seedStrength = 0.2f;
        protected float maxStrength = 2f;
        protected float energyConversation = 0.5f;

        // Примерный лимит популяции вида. При его превышении возникает случайный стресс от перенаселения
        protected int populationLimit = 50000;
        protected float overpopulationStressWeight = 0.05f;

        public override void AddResources(ref FoliageInstance instance, float energy, float water, float light) {
            // Сокращаем поступающую энергию в два раза для баланса
            float incomingEnergy = energy * 0.5f;

            instance.energy += incomingEnergy;

            // Дерево тратит энергию на выживание
            instance.energy -= requiredEnergy;

            if (instance.energy < 0f) {
                // Нехватка энергии конвертируется в стресс
                instance.stress += energyStressWeight * Mathf.Abs(instance.energy);
                // Энергия не может быть отрицательной (иначе она никогда не восстановится)
                instance.energy = 0f;
            } else {
                // При профиците энергии стресс постепенно спадает
                if (instance.stress > 0f) {
                    instance.stress = Mathf.Max(0f, instance.stress - incomingEnergy * 0.2f);
                }
                // Ограничиваем запас максимальной энергии (кап), чтобы он не рос бесконечно
                float maxEnergyCap = requiredEnergy * 10f;
                if (instance.energy > maxEnergyCap) {
                    instance.energy = maxEnergyCap;
                }
            }

            instance.stress += waterStressWeight * Mathf.Max(0f, requiredWater - water);
            // instance.stress += lightStressWeight * Mathf.Max(0f, requiredLight - light);

            // Механика угнетения при перенаселении вида (мягкое ограничение численности)
            if (Count > populationLimit) {
                float excessRatio = (float)Count / populationLimit - 1f;
                // Используем хеширование координат для получения псевдослучайного числа [0..1],
                // так как UnityEngine.Random не потокобезопасен (на случай использования Jobs)
                float pseudoRandom = Mathf.Abs(Mathf.Sin(instance.position.x * 12.989f + instance.position.y * 78.233f) * 43758.545f) % 1f;
                instance.stress += overpopulationStressWeight * excessRatio * pseudoRandom;
            }
        }

        public override bool Alive(in FoliageInstance instance) {
            // Убрали проверку instance.energy > 0, чтобы дерево не гибло в один клик от 0 энергии.
            // Оно получает стресс от нулей и умирает когда стресс выходит за лимит.
            return instance.type switch {
                FoliageInstance.FoliageType.Seed => instance.stress < acceptableSeedStress,
                FoliageInstance.FoliageType.Sapling => instance.stress < acceptableSaplingStress,
                FoliageInstance.FoliageType.Mature => instance.stress < acceptableMatureStress,
                _ => false,
            };
        }

        public override FoliageInstance CreateSeed(Vector2 position) {
            return new() {
                type = FoliageInstance.FoliageType.Seed,
                position = position,
                energy = seedEnergy,
                strength = seedStrength,
                stress = 0f,
            };
        }

        private void UpdateType(ref FoliageInstance instance) {
            if (instance.age < saplingStartAge) instance.type = FoliageInstance.FoliageType.Seed;
            else if (instance.age < matureStartAge) instance.type = FoliageInstance.FoliageType.Sapling;
            else instance.type = FoliageInstance.FoliageType.Mature;
        }

        public override void Grow(ref FoliageInstance instance) {
            instance.age += 1f;
            UpdateType(ref instance);

            instance.strength += energyConversation * instance.energy;
            // Жёсткое ограничение максимальной силы.
            // Иначе дерево бесконечно увеличивает свою ячейку Вороного, забирая все ресурсы карты!
            if (instance.strength > maxStrength) {
                instance.strength = maxStrength;
            }
        }

        public override FoliageInstance[] Seed(ref FoliageInstance instance) {
            int count = instance.type == FoliageInstance.FoliageType.Mature ? seedSpreadCount : 0;

            // Уменьшение количества семян пропорционально стрессу
            if (count > 0 && acceptableMatureStress > 0f) {
                float stressRatio = instance.stress / acceptableMatureStress;
                if (stressRatio > 0.8f) {
                    count = 0; // Дерево слишком истощено для плодоношения
                } else if (stressRatio > 0f) {
                    // Чем больше стресс, тем меньше семян
                    float yield = count * (1f - stressRatio);
                    // Вероятностно определяем конечное число
                    count = Mathf.FloorToInt(yield) + (Simulation.Random.Chance(yield % 1f) ? 1 : 0);
                }
            }

            var seeds = new FoliageInstance[count];

            for (int i = 0; i < seeds.Length; i++) {
                seeds[i] = CreateSeed(
                    Simulation.Random.NextGaussian(seedSpreadRadius, instance.position)
                );
            }

            return seeds;
        }
    }

    public class OakDescriptor : RuntimeSpeciesDescriptor {
        public OakDescriptor() {
            // Климаксный вид: теневынослив, нетребователен к свету, медленно растет
            energyStressWeight = 0.5f;
            requiredEnergy = 0.6f; // НИЗКАЯ потребность (выживает в тени под пологом)
            waterStressWeight = 0.5f;
            requiredWater = 0.8f;

            acceptableSeedStress = 2.0f; // Почти невозможно убить росток стрессом
            acceptableSaplingStress = 2.0f;
            acceptableMatureStress = 2.5f;

            seedEnergy = 4.0f; // Большой стартовый запас
            saplingStartAge = 8f;
            matureStartAge = 25f; // Снижено, чтобы успевали дать потомство
            seedSpreadRadius = 150f;
            seedSpreadCount = 1;
            populationLimit = 150;
            maxStrength = 8f; // Дуб выдавливает конкурентов
        }
    }

    public class PineDescriptor : RuntimeSpeciesDescriptor {
        public PineDescriptor() {
            // Переходный/Пионерный вид: светолюбив, но засухоустойчив
            energyStressWeight = 1.2f;
            requiredEnergy = 1.2f; // Нуждается в свете
            waterStressWeight = 0.4f;
            requiredWater = 0.5f;

            acceptableSeedStress = 0.8f;
            acceptableSaplingStress = 1.0f;
            acceptableMatureStress = 1.2f;

            seedEnergy = 2.0f;
            saplingStartAge = 6f;
            matureStartAge = 15f;
            seedSpreadRadius = 250f;
            seedSpreadCount = 2;
            populationLimit = 1500;
        }
    }

    public class BirchDescriptor : RuntimeSpeciesDescriptor {
        public BirchDescriptor() {
            // Пионер: требует очень много света, быстро вытесняется другими деревьями
            energyStressWeight = 3.0f; // Умножает стресс в тени х3
            requiredEnergy = 1.5f; // Очень высокая потребность в энергии
            waterStressWeight = 1.5f;
            requiredWater = 1.2f;

            // Как только дереву перестает хватать света или воды, оно почти мгновенно умирает:
            acceptableSeedStress = 0.2f;
            acceptableSaplingStress = 0.4f;
            acceptableMatureStress = 0.5f;

            seedEnergy = 0.5f;
            saplingStartAge = 5f;
            matureStartAge = 12f;
            seedSpreadRadius = 250f; // Урезал радиус, чтобы локализовать очаги
            seedSpreadCount = 1; // Убрал геометрическую прогрессию

            populationLimit = 2000;
            overpopulationStressWeight = 8.0f; // Смертельный стресс при перенаселении
        }
    }

    public class SpruceDescriptor : RuntimeSpeciesDescriptor {
        public SpruceDescriptor() {
            // Климаксный вид: крайне теневыносливая (минимальная потребность в энергии)
            energyStressWeight = 0.6f;
            requiredEnergy = 0.5f;
            waterStressWeight = 0.8f;
            requiredWater = 1.0f;

            acceptableSeedStress = 1.5f;
            acceptableSaplingStress = 1.8f;
            acceptableMatureStress = 2.0f;

            seedEnergy = 3.0f;
            saplingStartAge = 8f;
            matureStartAge = 20f;
            seedSpreadRadius = 150f;
            seedSpreadCount = 2;
            populationLimit = 2000;
            maxStrength = 6f; // Ель легко завоевывает пространство
        }
    }

    public class LindenDescriptor : RuntimeSpeciesDescriptor {
        public LindenDescriptor() {
            // Умеренная теневыносливость
            energyStressWeight = 0.8f;
            requiredEnergy = 0.8f;
            waterStressWeight = 0.8f;
            requiredWater = 1.0f;

            acceptableSeedStress = 1.0f;
            acceptableSaplingStress = 1.5f;
            acceptableMatureStress = 2.0f;

            seedEnergy = 3.0f;
            saplingStartAge = 8f;
            matureStartAge = 22f;
            seedSpreadRadius = 150f;
            seedSpreadCount = 1;
            populationLimit = 400;
        }
    }

    public class BushDescriptor : RuntimeSpeciesDescriptor {
        public BushDescriptor() {
            // Теневой подлесок: выживает на остатках ресурсов
            energyStressWeight = 0.5f;
            requiredEnergy = 0.3f;
            waterStressWeight = 0.5f;
            requiredWater = 0.3f;

            acceptableSeedStress = 1.5f;
            acceptableSaplingStress = 1.5f;
            acceptableMatureStress = 2.0f;

            seedEnergy = 1.0f;
            saplingStartAge = 2f;
            matureStartAge = 8f;
            seedSpreadRadius = 50f;
            seedSpreadCount = 2;
            populationLimit = 6000;
        }
    }
}
