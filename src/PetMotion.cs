using System;
using System.Drawing;

namespace HainanTurtlePet
{
    internal sealed class PetMotion
    {
        internal Rectangle Area;
        internal Size WindowSize;
        internal double X, Y;
        internal int Direction = -1;
        internal bool Falling;
        private double fallSpeed;
        internal int Floor { get { return Math.Max(Area.Top, Area.Bottom - WindowSize.Height); } }
        internal int Right { get { return Math.Max(Area.Left, Area.Right - WindowSize.Width); } }
        internal Point Location { get { return new Point((int)Math.Round(X), (int)Math.Round(Y)); } }

        internal PetMotion(Rectangle area, Size size, Point location)
        { Area = area; WindowSize = size; MoveTo(location); }

        internal void MoveTo(Point location)
        {
            X = Math.Max(Area.Left, Math.Min(Right, location.X));
            Y = Math.Max(Area.Top, Math.Min(Floor, location.Y));
        }

        internal void Drop() { Falling = true; fallSpeed = 0; }

        internal void Advance(double seconds, bool walk)
        {
            seconds = Math.Max(0, Math.Min(0.1, seconds));
            if (Falling)
            {
                fallSpeed += 780 * seconds;
                Y += fallSpeed * seconds;
                if (Y >= Floor) { Y = Floor; Falling = false; fallSpeed = 0; }
            }
            else if (walk)
            {
                X += Direction * 54 * seconds;
                if (X <= Area.Left) { X = Area.Left; Direction = 1; }
                if (X >= Right) { X = Right; Direction = -1; }
            }
        }
    }
}
