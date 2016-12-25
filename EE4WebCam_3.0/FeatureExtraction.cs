using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace EE4WebCam
{
    public class FeatureExtraction
    {
        public static int K = 2;
        public static double PERCENT_PASS = 0.8;
        public static double UNIQUENESS_THRESHOLD = 0.8;
        public static double HESSIAN_THRESH = 300;

        public static void ExtractFeature(Emgu.CV.Image<Gray, Byte> image, out VectorOfKeyPoint keyPoints, out UMat descriptors)
        {
            keyPoints = new VectorOfKeyPoint();
            descriptors = new UMat();

            if (CudaInvoke.HasCuda)
            {
                CudaSURF surfCuda = new CudaSURF((float)HESSIAN_THRESH);
                using (GpuMat gpuImage = new GpuMat(image))
                using (GpuMat gpuKeyPoints = surfCuda.DetectKeyPointsRaw(gpuImage, null))
                using (GpuMat gpuDescriptors = surfCuda.ComputeDescriptorsRaw(gpuImage, null, gpuKeyPoints))
                {
                    surfCuda.DownloadKeypoints(gpuKeyPoints, keyPoints);
                    descriptors = gpuDescriptors.ToMat().ToUMat(AccessType.Read);
                }
            }
            else
            {
                using (UMat uMatImage = image.ToUMat())
                {
                    SURF surfCPU = new SURF(HESSIAN_THRESH);
                    //extract features from the object image                    
                    surfCPU.DetectAndCompute(image, null, keyPoints, descriptors, false);
                }
            }
        }

        public static void FindMatch(Mat modelImage, Mat observedImage, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, out Mat mask, out Mat homography)
        {
            int k = 2;
            double uniquenessThreshold = 0.8;
            double hessianThresh = 300;

            homography = null;

            modelKeyPoints = new VectorOfKeyPoint();
            observedKeyPoints = new VectorOfKeyPoint();

            if (CudaInvoke.HasCuda)
            {
                CudaSURF surfCuda = new CudaSURF((float)hessianThresh);
                using (GpuMat gpuModelImage = new GpuMat(modelImage))
                //extract features from the object image
                using (GpuMat gpuModelKeyPoints = surfCuda.DetectKeyPointsRaw(gpuModelImage, null))
                using (GpuMat gpuModelDescriptors = surfCuda.ComputeDescriptorsRaw(gpuModelImage, null, gpuModelKeyPoints))
                using (CudaBFMatcher matcher = new CudaBFMatcher(DistanceType.L2))
                {
                    surfCuda.DownloadKeypoints(gpuModelKeyPoints, modelKeyPoints);

                    // extract features from the observed image
                    using (GpuMat gpuObservedImage = new GpuMat(observedImage))
                    using (GpuMat gpuObservedKeyPoints = surfCuda.DetectKeyPointsRaw(gpuObservedImage, null))
                    using (GpuMat gpuObservedDescriptors = surfCuda.ComputeDescriptorsRaw(gpuObservedImage, null, gpuObservedKeyPoints))
                    //using (GpuMat tmp = new GpuMat())
                    //using (Stream stream = new Stream())
                    {
                        matcher.KnnMatch(gpuObservedDescriptors, gpuModelDescriptors, matches, k);

                        surfCuda.DownloadKeypoints(gpuObservedKeyPoints, observedKeyPoints);

                        mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
                        mask.SetTo(new MCvScalar(255));
                        Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);

                        int nonZeroCount = CvInvoke.CountNonZero(mask);
                        if (nonZeroCount >= 4)
                        {
                            nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints,
                               matches, mask, 1.5, 20);
                        }
                    }
                }
            }
            else
            {
                using (UMat uModelImage = modelImage.ToUMat(AccessType.Read))
                using (UMat uObservedImage = observedImage.ToUMat(AccessType.Read))
                {
                    SURF surfCPU = new SURF(hessianThresh);
                    //extract features from the object image
                    UMat modelDescriptors = new UMat();
                    surfCPU.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);

                    // extract features from the observed image
                    UMat observedDescriptors = new UMat();
                    surfCPU.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);
                    BFMatcher matcher = new BFMatcher(DistanceType.L2);
                    matcher.Add(modelDescriptors);

                    matcher.KnnMatch(observedDescriptors, matches, k, null);
                    mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
                    mask.SetTo(new MCvScalar(255));
                    Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);

                    int nonZeroCount = CvInvoke.CountNonZero(mask);
                    if (nonZeroCount >= 4)
                    {
                        nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints,
                           matches, mask, 1.5, 20);
                        if (nonZeroCount >= 4)
                            homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints,
                               observedKeyPoints, matches, mask, 2);
                    }
                }
            }
        }

        public static void Draw(Mat modelImage, Mat observedImage)
        {
            Mat homography;
            VectorOfKeyPoint modelKeyPoints;
            VectorOfKeyPoint observedKeyPoints;
            using (VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch())
            {
                Mat mask;
                FindMatch(modelImage, observedImage, out modelKeyPoints, out observedKeyPoints, matches,
                   out mask, out homography);

                //Draw the matched keypoints
                Mat result = new Mat();
                Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
                   matches, result, new MCvScalar(255, 255, 255), new MCvScalar(255, 255, 255), mask);

                CvInvoke.Imshow("result", result.ToImage<Bgr, Byte>());
            }
        }

        public static bool MatchFeatures(VectorOfKeyPoint keyPointsFirst, VectorOfKeyPoint keyPointsSecond, UMat descriptorsFirst, UMat descriptorsSecond) 
        {
            VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch();
            Mat mask = new Mat();            

            BFMatcher matcher = new BFMatcher(DistanceType.L2);
            matcher.Add(descriptorsFirst);
            matcher.KnnMatch(descriptorsSecond, matches, K, null);

            mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
            mask.SetTo(new MCvScalar(255));
            Features2DToolbox.VoteForUniqueness(matches, UNIQUENESS_THRESHOLD, mask);

            int nonZeroCount = CvInvoke.CountNonZero(mask);
            if (nonZeroCount >= 4)
            {
                nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(keyPointsFirst, keyPointsSecond,
                               matches, mask, 1.5, 20);
                if (nonZeroCount >= 4)
                {
                    Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(keyPointsFirst,
                        keyPointsSecond, matches, mask, 2);
                    if (CvInvoke.CountNonZero(mask) >= 26)
                    {
                        return true;
                    }                 
                }
                
            }

            return false;
        }
    }
}
