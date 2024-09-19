namespace Voronoi
{

    public static class HashHelper
    {
        public static int CombineHashCodes(params object[] values)
        {
            int hash = 17;
            foreach (var value in values)
            {
                hash = hash * 31 + (value?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }
}