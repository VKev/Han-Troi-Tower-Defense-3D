using System;
using TowerDefense3D.Towers;

namespace TowerDefense3D.Enemies
{
    public readonly struct ElementPair : IEquatable<ElementPair>
    {
        public ElementPair(ElementType first, ElementType second)
        {
            if ((int)first <= (int)second)
            {
                First = first;
                Second = second;
            }
            else
            {
                First = second;
                Second = first;
            }
        }

        public ElementType First { get; }
        public ElementType Second { get; }

        public bool Equals(ElementPair other)
        {
            return First == other.First && Second == other.Second;
        }

        public override bool Equals(object obj)
        {
            return obj is ElementPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)First, (int)Second);
        }

        public override string ToString()
        {
            return $"{First} + {Second}";
        }
    }
}
