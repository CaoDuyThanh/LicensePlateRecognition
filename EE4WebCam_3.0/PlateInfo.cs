using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EE4WebCam
{
    public class PlateInfo
    {
        public Image<Bgr, Byte> Image;
        public VectorOfKeyPoint KeyPoints;
        public UMat Descriptors;
        public DateTime TimeIn;
        public string LicensePlate;

        public PlateInfo(Image<Bgr, Byte> image,
                         VectorOfKeyPoint keyPoints,
                         UMat descriptors,
                         DateTime timeIn){
            Image = image;
            KeyPoints = keyPoints;
            Descriptors = descriptors;
            TimeIn = timeIn;
        }

        public PlateInfo(Image<Bgr, Byte> image,
                         VectorOfKeyPoint keyPoints,
                         UMat descriptors,
                         DateTime timeIn,
                         string licensePlate)
        {
            Image = image;
            KeyPoints = keyPoints;
            Descriptors = descriptors;
            TimeIn = timeIn;
            LicensePlate = licensePlate;
        }

        public void ComputeFeatures()
        {
            Image<Gray, Byte> imGray = new Image<Gray, Byte>(Image.Bitmap);
            FeatureExtraction.ExtractFeature(imGray, out KeyPoints, out Descriptors);
        }

        public bool IsSimilar(PlateInfo anotherPlate)
        {
            return FeatureExtraction.MatchFeatures(KeyPoints, anotherPlate.KeyPoints,  Descriptors, anotherPlate.Descriptors);
        }
    }
}
