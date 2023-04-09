namespace HEAL.EquationSearch {
  internal class Hash {

    // A bitwise hash function written by Justin Sobel. hash functions adapted from http://partow.net/programming/hashfunctions/index.html#AvailableHashFunctions 
    public static ulong JSHash(byte[] input) {
      ulong hash = 1315423911;
      for (int i = 0; i < input.Length; ++i)
        hash ^= (hash << 5) + input[i] + (hash >> 2);
      return hash;
    }

    // same as above but for ulong inputs
    public static ulong JSHash(ulong[] input) {
      var bytes = new byte[input.Length * sizeof(ulong)];
      Buffer.BlockCopy(input, 0, bytes, 0, bytes.Length);
      return JSHash(bytes);
    }

    public static ulong JSHash(ulong[] inputArr, ulong input) {
      var bytes = new byte[(inputArr.Length + 1) * sizeof(ulong)];
      Buffer.BlockCopy(inputArr, 0, bytes, 0, inputArr.Length * sizeof(ulong));
      Buffer.BlockCopy(BitConverter.GetBytes(input), 0, bytes, dstOffset: inputArr.Length * sizeof(ulong), count: sizeof(ulong));
      return JSHash(bytes);
    }
  }
}
