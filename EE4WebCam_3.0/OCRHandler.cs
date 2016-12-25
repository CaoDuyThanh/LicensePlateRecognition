using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.ML;
using Emgu.CV.OCR;
using Emgu.CV.Structure;
using Emgu.CV.Text;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace EE4WebCam
{
    public class OCRHandler
    {
        public static int GAUSSIAN_SMOOTH_FILTER_SIZE = 19;
        public static int ADAPTIVE_THRESH_BLOCK_SIZE = 171;
        public static int ADAPTIVE_THRESH_WEIGHT = 15;
        int RESIZED_CHAR_IMAGE_WIDTH = 20;
        int RESIZED_CHAR_IMAGE_HEIGHT = 30;
        double MIN_WH_RATIO_CHARACTER = 0.2;
        double MAX_WH_RATIO_CHARACTER = 0.9;
        static int MIN_CONTOUR_AREA = 1800;
        static int MAX_CONTOUR_AREA = 5200;
        double OVERLAP_IGNORE = 0.65;

        public List<Image<Bgr, Byte>> DetectedPlates = null;
        public CascadeClassifier cascadeClassifierPlate = null;

        private Tesseract ocr = null;
        private KNearest kNearest = null;

        public OCRHandler()
        {
            ocr = new Tesseract(".\\tessdata", "eng", OcrEngineMode.TesseractCubeCombined);
            ocr.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890");
            ocr.PageSegMode = PageSegMode.SingleChar;

            cascadeClassifierPlate = new CascadeClassifier("output-hv-33-x25.xml");

            loadKNNDataAndTrainKNN();
        }

        #region PUBLIC FUNCTIONS

        public List<string> RecognizeCharInScene(Image<Bgr, Byte> image){
            List<string> detectedListCharsPlate = new List<string>();

            Image<Gray, Byte> grayScale = null;
            toGray(image, ref grayScale);
            DetectedPlates = detectLicensePlate(image, grayScale);
            
            foreach (Image<Bgr, Byte> plate in DetectedPlates)
            {
                // Recognize first plate detected
                string chars = recognizeCharsInPlateCon(plate);
                chars = preprocessCharsDetected(chars);
                detectedListCharsPlate.Add(chars);
                break;
            }

            return detectedListCharsPlate;
        }
            
        #endregion

        #region PRIVATE FUNCTION

        private string preprocessCharsDetected(string input)
        {
            char[] chars = input.ToCharArray();
            for (int i = 4; i < input.Length; i++)
            {
                if (chars[i] == 'L')
                {
                    chars[i] = '4';
                }
                if (chars[i] == 'f')
                {
                    chars[i] = '6';
                }
                if (chars[i] == '\'')
                {
                    chars[i] = '9';
                }
                if (chars[i] == 't')
                {
                    chars[i] = '4';
                }
            }
            return new string(chars);
        }

        private void toGray(Image<Bgr, Byte> img, ref Image<Gray, Byte> grayScale)
        {
            Image<Hsv, byte> imgHSV = img.Convert<Hsv, Byte>();
            Image<Gray, byte>[] imgChannels = imgHSV.Split();
            grayScale = imgChannels[2];
        }

        private bool loadKNNDataAndTrainKNN()
        {
            // note: we effectively have to read the first XML file twice
            // first, we read the file to get the number of rows (which is the same as the number of samples)
            // the first time reading the file we can't get the data yet, since we don't know how many rows of data there are
            // next, reinstantiate our classifications Matrix and training images Matrix with the correct number of rows
            // then, read the file again and this time read the data into our resized classifications Matrix and training images Matrix
            Matrix<float> mtxClassifications = new Matrix<float>(1, 1);
            // for the first time through, declare these to be 1 row by 1 column
            Matrix<float> mtxTrainingImages = new Matrix<float>(1, 1);
            // we will resize these when we know the number of rows (i.e. number of training samples)
            XmlSerializer xmlSerializer = new XmlSerializer(mtxClassifications.GetType());
            // these variables are for
            StreamReader streamReader;
            // reading from the XML files
            try
            {
                streamReader = new StreamReader("classifications.xml");
                // attempt to open classifications file
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }

            // read from the classifications file the 1st time, this is only to get the number of rows, not the actual data
            mtxClassifications = ((Emgu.CV.Matrix<float>)(xmlSerializer.Deserialize(streamReader)));
            streamReader.Close();
            // close the classifications XML file
            int intNumberOfTrainingSamples = mtxClassifications.Rows;
            // get the number of rows, i.e. the number of training samples
            // now that we know the number of rows, reinstantiate classifications Matrix and training images Matrix with the actual number of rows
            mtxClassifications = new Matrix<float>(intNumberOfTrainingSamples, 1);
            mtxTrainingImages = new Matrix<float>(intNumberOfTrainingSamples, (RESIZED_CHAR_IMAGE_WIDTH * RESIZED_CHAR_IMAGE_HEIGHT));
            try
            {
                streamReader = new StreamReader("classifications.xml");
                // reinitialize the stream reader
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }

            // read from the classifications file again, this time we can get the actual data
            mtxClassifications = ((Emgu.CV.Matrix<float>)(xmlSerializer.Deserialize(streamReader)));
            streamReader.Close();
            // close the classifications XML file
            xmlSerializer = new XmlSerializer(mtxTrainingImages.GetType());
            // reinstantiate file reading variables
            try
            {
                streamReader = new StreamReader("images.xml");
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }

            mtxTrainingImages = ((Emgu.CV.Matrix<float>)(xmlSerializer.Deserialize(streamReader)));
            streamReader.Close();
            // close the training images XML file
            //  train '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
            kNearest = new KNearest();
            // instantiate KNN object
            kNearest.Train(mtxTrainingImages, Emgu.CV.ML.MlEnum.DataLayoutType.RowSample, mtxClassifications);
            // call to train
            return true;
        }

        private void processImage(Image<Bgr, Byte> img, ref Image<Gray, Byte> grayScale, ref Image<Gray, Byte> imgRed, ref Image<Gray, Byte> imgGreen, ref Image<Gray, Byte> imgBlue)
        {
            Image<Hsv, byte> imgHSV = img.Convert<Hsv, Byte>();
            Image<Gray, byte>[] imgChannels = imgHSV.Split();
            grayScale = imgChannels[2];

            Image<Gray, byte>[] imgChannels1 = img.Split();
            imgBlue = imgChannels1[0];
            imgGreen = imgChannels1[1];
            imgRed = imgChannels1[2];
        }

        private List<PossibleChar> reorderCharacter(Image<Gray, Byte> img, List<PossibleChar> listOfPossibleChars)
        {
            listOfPossibleChars.Sort(delegate(PossibleChar oneChar, PossibleChar otherChar)
            {
                return oneChar.boundingRect.Y.CompareTo(otherChar.boundingRect.Y);
            });

            List<PossibleChar> listOfOrderPossibleChars = new List<PossibleChar>();
            List<PossibleChar> firstLine = new List<PossibleChar>();
            List<PossibleChar> secondLine = new List<PossibleChar>();
            foreach (PossibleChar character in listOfPossibleChars)
            {
                double intersectY = Math.Max(0, Math.Min(img.Height / 2, character.boundingRect.Y + character.boundingRect.Height) -
                                                Math.Max(1, character.boundingRect.Y));
                if (intersectY / character.boundingRect.Height > 0.7)
                {
                    firstLine.Add(character);
                }
                else
                {
                    secondLine.Add(character);
                }
            }

            firstLine.Sort(delegate(PossibleChar oneChar, PossibleChar otherChar)
            {
                return oneChar.boundingRect.X.CompareTo(otherChar.boundingRect.X);
            });

            secondLine.Sort(delegate(PossibleChar oneChar, PossibleChar otherChar)
            {
                return oneChar.boundingRect.X.CompareTo(otherChar.boundingRect.X);
            });

            foreach (PossibleChar character in firstLine)
            {
                listOfOrderPossibleChars.Add(character);
            }

            foreach (PossibleChar character in secondLine)
            {
                listOfOrderPossibleChars.Add(character);
            }

            return listOfOrderPossibleChars;
        }

        private double calOverlap(Rectangle firstRect, Rectangle secondRect, ref bool index)
        {
            double firstArea = firstRect.Width * firstRect.Height;
            double secondArea = secondRect.Width * secondRect.Height;

            double intersectX = Math.Max(0, Math.Min(firstRect.X + firstRect.Width, secondRect.X + secondRect.Width) -
                                            Math.Max(firstRect.X, secondRect.X));
            double intersectY = Math.Max(0, Math.Min(firstRect.Y + firstRect.Height, secondRect.Y + secondRect.Height) -
                                            Math.Max(firstRect.Y, secondRect.Y));
            double intersectArea = intersectX * intersectY;

            if (firstArea < secondArea)
            {
                return intersectArea / firstArea;
            }
            else
            {
                index = false;
                return intersectArea / secondArea;
            }

        }

        private List<PossibleChar> removeOverlap(List<PossibleChar> listOfPossibleChar)
        {
            List<bool> check = new List<bool>();
            for (int i = 0; i < listOfPossibleChar.Count; i++)
            {
                check.Add(true);
            }

            for (int i = 0; i < check.Count; i++)
            {
                if (check[i]){
                    for (int j = i + 1; j < check.Count; j++){
                        if (check[j]){
                            bool index = true;
                            double overlap = calOverlap(listOfPossibleChar[i].boundingRect, listOfPossibleChar[j].boundingRect, ref index);
                            if (overlap >= OVERLAP_IGNORE)
                            {
                                if (index)
                                {
                                    check[i] = false;
                                }
                                else
                                {
                                    check[j] = false;
                                }
                            }
                        }
                    }
                    
                }
            }

            List<PossibleChar> listOfRemovedOverlapChar = new List<PossibleChar>();
            for (int i = 0; i < check.Count; i++)
            {
                if (check[i])
                {
                    listOfRemovedOverlapChar.Add(listOfPossibleChar[i]);
                }
            }
            return listOfRemovedOverlapChar;
        }

        public static Image<Gray, byte> maximizeContrast(Image<Gray, byte> imgGrayscale)
        {
            Image<Gray, byte> imgTopHat;
            Image<Gray, byte> imgBlackHat;
            Image<Gray, byte> imgGrayscalePlusTopHat;
            Image<Gray, byte> imgGrayscalePlusTopHatMinusBlackHat;
            Mat kernel1 = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Cross, new Size(3, 3), new Point(1, 1));
            Matrix<byte> kernel2 = new Matrix<byte>(new Byte[3, 3] { { 0, 1, 0 }, { 1, 0, 1 }, { 0, 1, 0 } });
            imgTopHat = imgGrayscale.MorphologyEx(MorphOp.Gradient, kernel1, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());
            imgBlackHat = imgGrayscale.MorphologyEx(MorphOp.Gradient, kernel2, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());
            imgGrayscalePlusTopHat = (imgGrayscale + imgTopHat);
            imgGrayscalePlusTopHatMinusBlackHat = (imgGrayscalePlusTopHat - imgBlackHat);
            return imgGrayscalePlusTopHatMinusBlackHat;
        }

        private void preprocess(Image<Bgr, Byte> img, ref Image<Gray, Byte> grayScale, ref Image<Gray, Byte> imgThres)
        {
            toGray(img, ref grayScale);

            Image<Gray, byte> imgMaxContrastGrayscale = maximizeContrast(grayScale);
            Image<Gray, byte> imgBlurred = imgMaxContrastGrayscale.SmoothGaussian(GAUSSIAN_SMOOTH_FILTER_SIZE);
            imgThres = imgBlurred.ThresholdAdaptive(new Gray(255), AdaptiveThresholdType.GaussianC, ThresholdType.BinaryInv, ADAPTIVE_THRESH_BLOCK_SIZE, new Gray(ADAPTIVE_THRESH_WEIGHT));
        }


        private void getGradientMagnitude(Image<Gray, byte> imgGrayScale, ref Image<Gray, byte> imgGrad)
        {
            float[,] xFilter = { { -1, 0, 1 } };
            ConvolutionKernelF xKernel = new ConvolutionKernelF(xFilter);
            Image<Gray, float> imgDerivX = imgGrayScale * xKernel;

            float[,] yFilter = { { -1 }, { 0 }, { 1 } };
            ConvolutionKernelF yKernel = new ConvolutionKernelF(yFilter);
            Image<Gray, float> imgDerivY = imgGrayScale * yKernel;

            for (int i = 0; i < imgGrayScale.Size.Height; i++)
            {
                for (int j = 0; j < imgGrayScale.Size.Width; j++)
                {
                    imgGrad.Data[i, j, 0] = (byte)Math.Sqrt(imgDerivX.Data[i, j, 0] * imgDerivX.Data[i, j, 0] + imgDerivY.Data[i, j, 0] * imgDerivY.Data[i, j, 0]);
                }
            }
        }

        // Extract channels from image (Blue Green Red L Gradient)
        private void extractChannels(Image<Bgr, Byte> img, ref VectorOfMat channels)
        {
            // Extract Blue Green Red
            Image<Gray, byte>[] bgrChannels = img.Split();
            channels.Push(bgrChannels[0]);   // Add blue channel
            channels.Push(bgrChannels[1]);   // Add green channel
            channels.Push(bgrChannels[2]);   // Add red channel

            // Extract L channel
            Image<Hls, byte> imgHLS = new Image<Hls, byte>(img.Bitmap);
            Image<Gray, byte>[] hlsChannels = imgHLS.Split();
            channels.Push(hlsChannels[1]);   // Add L channel

            // Extract gradient magnitude
            Image<Gray, byte> imgGrayScale = new Image<Gray, byte>(img.Bitmap);
            Image<Gray, byte> imgGrad = new Image<Gray, byte>(img.Size);
            getGradientMagnitude(imgGrayScale, ref imgGrad);
            channels.Push(imgGrad);          // Add L channel
        }

        private string recognizeCharsInPlate(Image<Bgr, Byte> img)
        {
            string strChars = "";

            // Create erfilter
            ERFilterNM1 erfilterNM1 = new ERFilterNM1("trained_classifierNM1_1.xml", 5, (float)0.0025, 0.20f, 0.1f, true, 0.1f);
            ERFilterNM2 erfilterNM2 = new ERFilterNM2("trained_classifierNM2_1.xml", 0.3f);

            // Extract channels from images RGBLGradient
            VectorOfMat channels = new VectorOfMat();
            extractChannels(img, ref channels);

            // Extract ER
            VectorOfERStat[] vectorOutNM = new VectorOfERStat[channels.Size];
            for (int c = 0; c < channels.Size; c++)
            {
                if (vectorOutNM[c] == null)
                {
                    vectorOutNM[c] = new VectorOfERStat();
                }
                erfilterNM1.Run(channels[c], vectorOutNM[c]);
                erfilterNM2.Run(channels[c], vectorOutNM[c]);
            }

            Image<Bgr, byte> imgGrayColor = img.Clone();
            foreach (VectorOfERStat vc in vectorOutNM)
            {
                foreach (MCvERStat cv in vc.ToArray())
                {
                    imgGrayColor.Draw(cv.Rect, new Bgr(Color.Green), 2);
                }
            }
            CvInvoke.Imshow("ImgThreshColor", imgGrayColor);
             //Rectangle[] result = ERFilter.ERGrouping(img, channels, vectorOutNM, ERFilter.GroupingMethod.OrientationAny, "trained_classifier_erGrouping.xml", 0.3f);


             //Image<Bgr, byte> imgGrayColor = img.Clone();
             //foreach (Rectangle rect in result)
             //{
             //    imgGrayColor.Draw(rect, new Bgr(Color.Green), 2);
             //    //Image<Gray, byte> imgROI = imgThres.Copy(currentChar.boundingRect);
             //    //Image<Gray, byte> imgROIResized = imgROI.Resize(RESIZED_CHAR_IMAGE_WIDTH, RESIZED_CHAR_IMAGE_HEIGHT, Inter.Linear);
             //    //Matrix<float> mtxTemp = new Matrix<float>(imgROIResized.Size);
             //    //Matrix<float> mtxTempReshaped = new Matrix<float>(1, (RESIZED_CHAR_IMAGE_WIDTH * RESIZED_CHAR_IMAGE_HEIGHT));
             //    //CvInvoke.cvConvert(imgROIResized, mtxTemp);
             //    //for (int intRow = 0; intRow <= RESIZED_CHAR_IMAGE_HEIGHT - 1; intRow++)
             //    //{
             //    //    // flatten Matrix into one row by RESIZED_IMAGE_WIDTH * RESIZED_IMAGE_HEIGHT number of columns
             //    //    for (int intCol = 0; intCol <= RESIZED_CHAR_IMAGE_WIDTH - 1; intCol++)
             //    //    {
             //    //        mtxTempReshaped[0, intRow * RESIZED_CHAR_IMAGE_WIDTH + intCol] = mtxTemp[intRow, intCol];
             //    //    }
             //    //}

                 

             //    //    float sngCurrentChar = kNearest.Predict(mtxTempReshaped);
             //    //  strChars = (strChars + ((char)(Convert.ToInt32(sngCurrentChar))));
             //}
             
            //List<PossibleChar> listOfPossibleChars = new List<PossibleChar>();

            
            //VectorOfERStat vectorOut = new VectorOfERStat();
            //erfilterNM1.Run(imgGrayScale, vectorOut);
            //for (int i = 0; i < vectorOut.Size; i++)
            //{
            //    MCvERStat vec = vectorOut[i];

            //    double ratio = vec.Rect.Width * 1.0 / vec.Rect.Height;
            //    if (vec.Area >= MIN_CONTOUR_AREA && vec.Area <= MAX_CONTOUR_AREA && MIN_WH_RATIO_CHARACTER <= ratio && ratio <= MAX_WH_RATIO_CHARACTER)
            //    {
            //        listOfPossibleChars.Add(new PossibleChar(vec.Rect));
            //    }
            //}
            //VectorOfERStat vectorOut1 = new VectorOfERStat();
            //erfilterNM1.Run(imgRed, vectorOut1);
            //for (int i = 0; i < vectorOut1.Size; i++)
            //{
            //    MCvERStat vec = vectorOut1[i];

            //    double ratio = vec.Rect.Width * 1.0 / vec.Rect.Height;
            //    if (vec.Area >= MIN_CONTOUR_AREA && vec.Area <= MAX_CONTOUR_AREA && MIN_WH_RATIO_CHARACTER <= ratio && ratio <= MAX_WH_RATIO_CHARACTER)
            //    {
            //        listOfPossibleChars.Add(new PossibleChar(vec.Rect));
            //    }
            //}
            //VectorOfERStat vectorOut2 = new VectorOfERStat();
            //erfilterNM1.Run(imgGreen, vectorOut2);
            //for (int i = 0; i < vectorOut2.Size; i++)
            //{
            //    MCvERStat vec = vectorOut2[i];

            //    double ratio = vec.Rect.Width * 1.0 / vec.Rect.Height;
            //    if (vec.Area >= MIN_CONTOUR_AREA && vec.Area <= MAX_CONTOUR_AREA && MIN_WH_RATIO_CHARACTER <= ratio && ratio <= MAX_WH_RATIO_CHARACTER)
            //    {
            //        listOfPossibleChars.Add(new PossibleChar(vec.Rect));
            //    }
            //}
            //VectorOfERStat vectorOut3 = new VectorOfERStat();
            //erfilterNM1.Run(imgBlue, vectorOut3);
            //for (int i = 0; i < vectorOut3.Size; i++)
            //{
            //    MCvERStat vec = vectorOut3[i];

            //    double ratio = vec.Rect.Width * 1.0 / vec.Rect.Height;
            //    if (vec.Area >= MIN_CONTOUR_AREA && vec.Area <= MAX_CONTOUR_AREA && MIN_WH_RATIO_CHARACTER <= ratio && ratio <= MAX_WH_RATIO_CHARACTER)
            //    {
            //        listOfPossibleChars.Add(new PossibleChar(vec.Rect));
            //    }
            //}

            //listOfPossibleChars = removeOverlap(listOfPossibleChars);

            //List<PossibleChar> listOfOrderPossibleChars = reorderCharacter(img, listOfPossibleChars);

            //Image<Bgr, byte> imgGrayColor = img.Clone();
            //foreach (PossibleChar currentChar in listOfOrderPossibleChars)
            //{
            //    imgGrayColor.Draw(currentChar.boundingRect, new Bgr(Color.Green), 2);
            //    //Image<Gray, byte> imgROI = imgThres.Copy(currentChar.boundingRect);
            //    //Image<Gray, byte> imgROIResized = imgROI.Resize(RESIZED_CHAR_IMAGE_WIDTH, RESIZED_CHAR_IMAGE_HEIGHT, Inter.Linear);
            //    //Matrix<float> mtxTemp = new Matrix<float>(imgROIResized.Size);
            //    //Matrix<float> mtxTempReshaped = new Matrix<float>(1, (RESIZED_CHAR_IMAGE_WIDTH * RESIZED_CHAR_IMAGE_HEIGHT));
            //    //CvInvoke.cvConvert(imgROIResized, mtxTemp);
            //    //for (int intRow = 0; intRow <= RESIZED_CHAR_IMAGE_HEIGHT - 1; intRow++)
            //    //{
            //    //    // flatten Matrix into one row by RESIZED_IMAGE_WIDTH * RESIZED_IMAGE_HEIGHT number of columns
            //    //    for (int intCol = 0; intCol <= RESIZED_CHAR_IMAGE_WIDTH - 1; intCol++)
            //    //    {
            //    //        mtxTempReshaped[0, intRow * RESIZED_CHAR_IMAGE_WIDTH + intCol] = mtxTemp[intRow, intCol];
            //    //    }
            //    //}

            //    CvInvoke.Imshow("ImgThreshColor", imgGrayColor);

            ////    float sngCurrentChar = kNearest.Predict(mtxTempReshaped);
            //  //  strChars = (strChars + ((char)(Convert.ToInt32(sngCurrentChar))));
            //}

            return strChars;
        }

        private List<Image<Bgr, Byte>> detectLicensePlate(Image<Bgr, Byte> img, Image<Gray, Byte> grayScale)
        {
            var foundPlates = cascadeClassifierPlate.DetectMultiScale(grayScale, 1.1, 8, Size.Empty);

            List<Image<Bgr, Byte>> detectedPlates = new List<Image<Bgr, byte>>();
            Image<Bgr, Byte> plateTemplate = img.Clone();
            int i = 0;
            foreach (var plate in foundPlates)
            {
                i++;
                Image<Bgr, Byte> tmp = plateTemplate.Copy();
                tmp.ROI = plate;
                //CvInvoke.Imshow("detectplate " + i.ToString(), tmp);

                detectedPlates.Add(tmp.Resize(500.0 / 274, Inter.Cubic));
            }

            return detectedPlates;
        }

        #endregion

        #region CONTOUR VER

        public static Image<Gray, byte> maximizeContrastCon(Image<Gray, byte> imgGrayscale)
        {
            Image<Gray, byte> imgTopHat;
            Image<Gray, byte> imgBlackHat;
            Image<Gray, byte> imgGrayscalePlusTopHat;
            Image<Gray, byte> imgGrayscalePlusTopHatMinusBlackHat;
            Mat kernel = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Rectangle, new Size(3, 3), new Point(1, 1));
            imgTopHat = imgGrayscale.MorphologyEx(Emgu.CV.CvEnum.MorphOp.Tophat, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());
            imgBlackHat = imgGrayscale.MorphologyEx(Emgu.CV.CvEnum.MorphOp.Blackhat, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());
            imgGrayscalePlusTopHat = (imgGrayscale + imgTopHat);
            imgGrayscalePlusTopHatMinusBlackHat = (imgGrayscalePlusTopHat - imgBlackHat);
            return imgGrayscalePlusTopHatMinusBlackHat;
        }

        private void processImageCon(Image<Bgr, Byte> img, ref Image<Gray, Byte> grayScale)
        {
            Image<Hsv, byte> imgHSV = img.Convert<Hsv, Byte>();
            Image<Gray, byte>[] imgChannels = imgHSV.Split();
            grayScale = imgChannels[2];
        }

        private void preprocessCon(Image<Bgr, Byte> img, ref Image<Gray, Byte> grayScale, ref Image<Gray, Byte> imgThres)
        {
            processImageCon(img, ref grayScale);

            Image<Gray, byte> imgMaxContrastGrayscale = maximizeContrast(grayScale);
            Image<Gray, byte> imgBlurred = imgMaxContrastGrayscale.SmoothGaussian(GAUSSIAN_SMOOTH_FILTER_SIZE);
            imgThres = imgBlurred.ThresholdAdaptive(new Gray(255), AdaptiveThresholdType.GaussianC, ThresholdType.BinaryInv, ADAPTIVE_THRESH_BLOCK_SIZE, new Gray(ADAPTIVE_THRESH_WEIGHT));
        }

        private string recognizeCharsInPlateCon(Image<Bgr, Byte> img)
        {
            string strChars = "";

            // Preprocessing - Convert grayscale, threshold grayscale
            Image<Gray, Byte> imgGrayScale = null;
            Image<Gray, Byte> imgThres = null;
            preprocess(img, ref imgGrayScale, ref imgThres);
            //CvInvoke.Imshow("ImgThreshold", imgThres);

            //// Find all possible character - use contour
            imgThres = imgThres.Resize(300.0 / imgThres.Width, Inter.Cubic);
            Image<Gray, byte> imgContours = new Image<Gray, byte>(imgThres.Size);
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(imgThres.Clone(), contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
            List<PossibleChar> listOfPossibleChars = new List<PossibleChar>();

            for (int i = 0; i < contours.Size; i++)
            {
                VectorOfPoint contour = contours[i];
                // get the current contour, note that the lower the multiplier, the higher the precision
                Rectangle rect = CvInvoke.BoundingRectangle(contour);
                double area = rect.Width * rect.Height;
                double ratio = rect.Width * 1.0 / rect.Height;
                if (area >= MIN_CONTOUR_AREA && area <= MAX_CONTOUR_AREA && MIN_WH_RATIO_CHARACTER <= ratio && ratio <= MAX_WH_RATIO_CHARACTER)
                {
                    listOfPossibleChars.Add(new PossibleChar(rect));
                }
            }

            // Find all possible character - using Haar-like feature
            CascadeClassifier cascadeClassifierCharacter = new CascadeClassifier("CharacterDetect.xml");
            var foundCharacters = cascadeClassifierCharacter.DetectMultiScale(imgThres, 1.05, 8, Size.Empty);            
            foreach (var character in foundCharacters)
            {
                double area = character.Width * character.Height;
                double ratio = character.Width * 1.0 / character.Height;
                if (area >= MIN_CONTOUR_AREA && area <= MAX_CONTOUR_AREA && MIN_WH_RATIO_CHARACTER <= ratio && ratio <= MAX_WH_RATIO_CHARACTER)
                {
                    listOfPossibleChars.Add(new PossibleChar(character));
                }
            }

            listOfPossibleChars = removeOverlap(listOfPossibleChars);

            List<PossibleChar> listOfOrderPossibleChars = reorderCharacter(imgThres, listOfPossibleChars);

            Image<Bgr, byte> imgThreshColor;
            imgThreshColor = imgThres.Convert<Bgr, Byte>();
            foreach (PossibleChar currentChar in listOfOrderPossibleChars)
            {
                //Rectangle rect = currentChar.boundingRect;
                //UMat imgROI = imgGrayScale.Copy(rect).ToUMat();
                //UMat imgROI1 = new UMat();

                ////resize the license plate such that the front is ~ 10-12. This size of front results in better accuracy from tesseract
                //Size approxSize = new Size(50, 50);
                //double scale = Math.Min(approxSize.Width * 1.0 / rect.Width, approxSize.Height * 1.0 / rect.Height);
                //Size newSize = new Size( (int)Math.Round(rect.Width*scale),(int) Math.Round(rect.Height*scale));
                //CvInvoke.Resize(imgROI, imgROI1, newSize, 0, 0, Inter.Cubic);

                ////removes some pixels from the edge
                //int edgePixelSize = 2;
                //Rectangle newRoi = new Rectangle(new Point(edgePixelSize, edgePixelSize), imgROI1.Size - new Size(2*edgePixelSize, 2*edgePixelSize));
                //UMat plate = new UMat(imgROI1, newRoi);

                //using (UMat tmp = plate.Clone())
                //{
                //    ocr.Recognize(tmp);
                //    Tesseract.Character[] character = ocr.GetCharacters();
                //    strChars = strChars + character;
                //}

                imgThreshColor.Draw(currentChar.boundingRect, new Bgr(Color.Green), 2);
                Image<Gray, byte> imgROI = imgThres.Copy(currentChar.boundingRect);
                Image<Gray, byte> imgROIResized = imgROI.Resize(RESIZED_CHAR_IMAGE_WIDTH, RESIZED_CHAR_IMAGE_HEIGHT, Inter.Linear);

                //Image<Gray, byte> imgROIResized_Clone = imgROI.Clone();
                //CvInvoke.CopyMakeBorder(imgROIResized_Clone, imgROIResized_Clone, 5, 5, 5, 5, BorderType.Constant, new MCvScalar(0));
                //imgROIResized_Clone = imgROIResized_Clone.Resize(50, 50, Inter.Cubic);
                //CvInvoke.BitwiseNot(imgROIResized_Clone, imgROIResized_Clone);
                //using (UMat tmp = imgROIResized_Clone.ToUMat())
                //{
                //    ocr.Recognize(tmp);
                //    Tesseract.Character[] character = ocr.GetCharacters();
                //    strChars = strChars + character[0].Text;
                //}

                Matrix<byte> mtxTemp = new Matrix<byte>(imgROIResized.Size);
                Matrix<float> mtxTempReshaped = new Matrix<float>(1, (RESIZED_CHAR_IMAGE_WIDTH * RESIZED_CHAR_IMAGE_HEIGHT));
                imgROIResized.CopyTo(mtxTemp);
                for (int intRow = 0; intRow <= RESIZED_CHAR_IMAGE_HEIGHT - 1; intRow++)
                {
                    // flatten Matrix into one row by RESIZED_IMAGE_WIDTH * RESIZED_IMAGE_HEIGHT number of columns
                    for (int intCol = 0; intCol <= RESIZED_CHAR_IMAGE_WIDTH - 1; intCol++)
                    {
                        mtxTempReshaped[0, intRow * RESIZED_CHAR_IMAGE_WIDTH + intCol] = imgROIResized.Data[intRow, intCol, 0];
                    }
                }

                //CvInvoke.Imshow("ImgThreshColor", imgThreshColor);

                float sngCurrentChar = kNearest.Predict(mtxTempReshaped);
                strChars = (strChars + ((char)(Convert.ToInt32(sngCurrentChar))));
            }

            return strChars;
        }


        #endregion
    }
}
