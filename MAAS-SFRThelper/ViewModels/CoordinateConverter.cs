namespace MAAS_SFRThelper.ViewModels
{
    internal class CoordinateConverter
    {
        public double PointToCanvas(double coordinate)
        {
            return (coordinate + constant) * multiplier;
        }

        public double PointFromCanvas(double canvasPoint)
        {
            return canvasPoint / multiplier - constant;
        }

        public double LengthToCanvas(double length)
        {
            return length * multiplier;
        }

        public CoordinateConverter(double minVal, double maxVal, double canvasWidth)
        {
            constant = -minVal;
            multiplier = canvasWidth / (maxVal - minVal);
        }

        readonly double multiplier;
        readonly double constant;
    }
}
