using UnityEngine;

namespace LucentPath.Tools
{
    public static class HashHelper
    {
        public static int CreateHash()
        {
            unchecked
            {
                return 17;
            }
        }

        public static int AppendHash(int hash, int value)
        {
            unchecked
            {
                return hash * 31 + value;
            }
        }

        public static int AppendHash(int hash, float value)
        {
            return AppendHash(hash, value.GetHashCode());
        }

        public static int AppendHash(int hash, Color value)
        {
            hash = AppendHash(hash, value.r);
            hash = AppendHash(hash, value.g);
            hash = AppendHash(hash, value.b);

            return AppendHash(hash, value.a);
        }
    }
}
