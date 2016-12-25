using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace EE4WebCam
{
    public class PossibleChar
    {
        public Rectangle boundingRect;
        public long lngCenterX;
        public long lngCenterY;
        public double dblDiagonalSize;
        public double dblAspectRatio;
        public long lngArea;

        //  constructor '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        public PossibleChar(Rectangle rect)
        {
            boundingRect = rect;
            lngCenterX = (long)((boundingRect.Left + boundingRect.Right) / 2);
            lngCenterY = (long)((boundingRect.Top + boundingRect.Bottom) / 2);
            dblDiagonalSize = Math.Sqrt(boundingRect.Width * boundingRect.Width + boundingRect.Height * boundingRect.Height);
            dblAspectRatio = (double)(boundingRect.Width) / (double)(boundingRect.Height);
            lngArea = (boundingRect.Width * boundingRect.Height);
        }
    }
}
