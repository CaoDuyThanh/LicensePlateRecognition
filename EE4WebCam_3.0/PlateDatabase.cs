using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EE4WebCam
{
    public class PlateDatabase
    {
        public static double CASH_PER_DAY = 30000;

        public List<PlateInfo> DB = null;

        public PlateDatabase()
        {
            DB = new List<PlateInfo>();
        }

        public bool IsExist(PlateInfo plateInput){
            foreach (PlateInfo plate in DB){
                if (plate.IsSimilar(plateInput)){
                    return true;
                }
            }
            return false;
        }

        public PlateInfo GetPlate(Image<Bgr, Byte> image)
        {
            PlateInfo tempPlate = new PlateInfo(image, new VectorOfKeyPoint(), new UMat(), DateTime.Now);
            tempPlate.ComputeFeatures();
            foreach (PlateInfo plate in DB)
            {
                Image<Gray, Byte> abc = new Image<Gray, Byte>(plate.Image.Bitmap);
                Image<Gray, Byte> abc1 = new Image<Gray, Byte>(image.Bitmap);

                if (plate.IsSimilar(tempPlate))
                {
                    return plate;
                }
            }
            return null;
        }

        public PlateInfo AddPlate(Image<Bgr, Byte> image, string licensePlate = "")
        {
            //PlateInfo newPlate = new PlateInfo(image, new VectorOfKeyPoint(), new UMat(), DateTime.Now);
            PlateInfo newPlate = new PlateInfo(image, new VectorOfKeyPoint(), new UMat(), DateTime.Now, licensePlate);
            newPlate.ComputeFeatures();
            DB.Add(newPlate);

            return newPlate;
        }

        public void RemovePlate(PlateInfo plate)
        {
            DB.Remove(plate);
        }

        public double GetFee(PlateInfo plate)
        {
            double fee = 0;
            DateTime currentTime = DateTime.Now;

            long timestamps = currentTime.Ticks - plate.TimeIn.Ticks;

            double days = Math.Floor(TimeSpan.FromTicks(timestamps).TotalDays); ;
            fee = days * CASH_PER_DAY;

            return fee;
        }
    }
}
