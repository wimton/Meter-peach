using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Peach.Core;
using Peach.Core.Dom;
using Peach.Core.Analyzers;


namespace Peach.Core.Test.Fixups
{
    [TestFixture]
    class SignatureFixupTests : DataModelCollector
    {
        public static byte[] FWkey ={
0x45,0x43,0x53,0x34,//type
0x30,0x00,0x00,0x00,//length,followed by x,y,d
0xf9,0x29,0x4d,0x62,0x3d,0x46,0x7c,0x93,0x99,0x59,0x20,0x73,0x3e,0xde,0xe0,0xcd,0x27,0xa4,0x5e,0x1d,0x4c,0xc8,0xa8,0x50,0xec,0xc3,0xfa,0xb4,0xc8,0x4e,0x44,0xba,0xbf,0x2e,0x51,0x31,0xea,0x77,0x6f,0x80,0x87,0xfc,0x95,0xe7,0x1b,0x31,0x11,0xcb,
0xfd,0x61,0x1c,0x20,0x3f,0x1c,0x13,0xdb,0xa8,0x47,0x2a,0x35,0xee,0x00,0x97,0x95,0x89,0xe7,0xef,0x6f,0x4c,0x69,0x47,0x1e,0xcb,0xce,0xc4,0x2a,0x28,0x27,0x07,0xa2,0x12,0x40,0xa1,0xec,0x1d,0x25,0x1d,0x41,0x86,0x02,0x4d,0xa8,0xfc,0xae,0x94,0xb3,

0xa4,0xc9,0xe7,0x66,0x31,0xa2,0xfa,0x58,0xf1,0x74,0x06,0x79,0xe7,0x03,0xd4,0xc7,0x2e,0x83,0x15,0x05,0x4e,0x29,0x25,0x1e,0x9b,0x7f,0x0f,0xd1,0x38,0x64,0x20,0xc3,0x00,0x3c,0xaa,0x1a,0x0a,0x51,0x1c,0xd3,0x1c,0x0a,0x81,0x2f,0x93,0x00,0x1a,0xd4
};
        public static string RSAkey =
@"-----BEGIN RSA PRIVATE KEY-----\
MIIEpAIBAAKCAQEAnCor34tDUB8COw5oiOvOl5tCiZKxWBOLBTnu/ve3/82W7xoo\
ph+wjt0QFfceo8axOJ2rOI9Exe8lTLCe/FFVhldL6LSv92H8Jq5fqs0DVGMLMb0r\
XMVq8wE8JjwM81+tqIBoipBNBwHlT1z6VHH+r3LJTcVpe7SQqcYp5t6VwSiKS5Ev\
wm+YeHUJwSKsXf26+DJrfXdk7066DqrXrzTMzX2x4aHvb4/dX7C848DI6X6joBod\
DbwKUEeuW0hAEOP7OhjOFaYqAyo+hmMHd97Jl0fWiajdPR0MPLnafxxqmqNcJM66\
QPQzGJu8E+KdS8yNYCE5mYAXSEwJvcqOMwaRewIDAQABAoIBAE/Q3gurSgQxVRqK\
CrOwki66lA9sgmfZ1TwemCCIy+paUcJzRENj/wGFyyru2yIp83pUW6bU0vm3eQDx\
ZNDhYS0AgTO25hkyY4YBqbPKhOEknhwV64vG+xqoju2b06KwTDnLJ6NqjXP/bAxc\
ITX37YwOxwSQ2ZD4gNfFCLWPHktSeJJLQIv4MBDcGnGD1nm+SRSqHkHp+tDmxez0\
ctQn66AoEWGOg7WP0azo6ESPXZTrxR1Wqd5lmlbrwaX2NvPuFHh7e8ZeU8/JWKQi\
kNF0/PPBJCkLN8xlAr/dfIAAU+iOQunFnGbb4tLiz7oDxZ0qgAfSQW+X5Rn2bQei\
k31isgECgYEAz9Lmdjwgp02lZ5z2klbD4IPYilQJffVe+7xFZFGj0i9/J9fvoXHY\
vgvGuID8Zsgk/dM7oqb71vnj57+3usrNp5trgf9tlXJZn4ltyrQsptaRCZqV7qaZ\
51a8azW9XJ4wd80HWVuJPplanUN2Ux0IIBtd/3w9uWTGACS9ynt0HdsCgYEAwF2c\
8jV5OKI//IbeNRJBmMQOKfNg18qKIpDNwooOcXnU1DC0piBLZYsa87an/0rqiaqp\
eXIQ5uA3sDd6C0ejcD6+/ulzA9uc5FeLm6Ag0HNSxgIYABPQ3Jd7ebMrqnDYi0Jg\
ECexcEHTOpjB5BGpurehWE3bTqZ6LwgGYcS7POECgYEAzcqJa4seyoyYvYEwqLhC\
PpBQXKnavF+9LonALRaqofdmco3hPHz/ozEGFq2jQiPufWouI2I2/yl0BIhT5yPr\
gYzlaFUGrnYNSW1MyuyfSpYuCNSKo0dWHz5EVeVhHdWHKRpdrJ53yQUSNagYAzU8\
Vo7DBbqBZJPlfT7ksRyOXwECgYBuSn38pMoOvX/gQldOqFvxwZ65ULAaqSaP8OP4\
AP2M9CQhUJeSk/uGib33M6eYiJR2P+IRHmQwayeiofwYUYeUiHUrZB+se5K1nLgP\
jzyhJy2zF2o5SSM8BqIlwaNsgmy1U2YOfSOP0D3SX9jy8WmWA0i/f0wZCPwO0RQP\
pCMSIQKBgQCKn9WwIDquJnX1IrhIACME0EjToledLFRBDv/YKNeKX3nShGhGrWpE\
3nVYe4sV1WVch9+hvh8kmVKyl3LJ2OwGAtXOG3p8Pa5XeF74tmsPjWxQXRMMuVSj\
QN6pLep3j3fAA/nhgpXEfNK4YvbEgnxYPjm8eb96fiNqrdzfKhnAkA==\
-----END RSA PRIVATE KEY-----";
    public static string ECkey = 
@"-----BEGIN PRIVATE KEY-----\
MIG2AgEAMBAGByqGSM49AgEGBSuBBAAiBIGeMIGbAgEBBDDZ2y3GNxOLcPN6COKc\
6PCCaQKgmTeFk8Fm75ZrGQmselsT/BQtq2GlCvumbAeg5G2hZANiAATb0O6O9LXV\
wv5FUO5FPj3WyWT2uEk1HW2dooCS4oEk/mZxmDI1x9y6LyhoDXOgQPMVKW9CaWoR\
pQbzZqTnpOfY69I6TDmUVkvHxsz3DQyIKWAWlVm30M1v/UMaoVT3lO0=\
-----END PRIVATE KEY-----";
        public static string XMLkey =
@"<RSAKeyValue><Modulus>1PP3imN+V4XeBr5/Gy1SWb9p6WzPmUuGyWDKuboM9SUQcAjfcZuE6/X0i9MwOGIinVVgIZfEDDq/VrjInN3t/Zh++UHItnNzV7hw9NcoVgIW
m5V4rgRWmeuRjXnkePoGgiWViJ+ejeReYF/OKeeb8axoGsgh7qkDr56uih6XtlwqCmn92/C9OmmNVfSMvrXtgeibm5hy99tBCfwu09seg3LnYmBG+ve/4573sG6Z/uEQW6j/
hdaafZ2y5rOOZC4aQUQucDmZF3oaxQWIsQxbDXrG+FifrUD3/gJrTWsoZap1MIlupolxH8uPJi8UT2j6tXUtWCvG3pvKNH+hZnHjDQ==</Modulus><Exponent>AQAB</Ex
ponent><P>8pGGuRNpadtGO+SFgpyqe40OpZ3rVafWQ3EIQgPj5cPiFgktCj2wayL4m83IcVste4N7QucrK7cdxdegImnTuaeRTDLy2MjVD0PJh0aVfl3mqWkA/axuaF2y1v
Z2USfP+fZ25Cuu+aDqpLw53mSFLJIwKOuXG2DhOHhs4uuLGwE=</P><Q>4L6iKH3Ak0Ix0gokE+cHl37E2oYYsATfD99odRXQgsb7gDj9O45tJmQhB3ADv97TuWRSJMyeRey
LTOHeFJwRl+EuBaB+OuDOPsZM4hZ6p6Ilco6x2JkMpV4SkZti5z0b32ZA8H1IjWjPTfZnMGOWU8SixNinfodFU7kcD191hA0=</Q><DP>8ORM4ufAO/wjF7+uRzPGqsQ+04K
s/3eadYd/J/AtFzUdBb0/GXiCByPHuRL4CHsABDVi5+IjIVrNGnk08ngeZ7VdukqumVN5I5uyO8GBHOmr9HnvHf1r+AF1Zb2Farsa86YsFBS9w/JmlArJfW5eQxE6+qhcfDg
N8sLFf+VjVAE=</DP><DQ>JzJYScOU4Jn84msOW8JBWrSrVIlqqNhQpw4Jw9HoKcbWekh2MfrnInj4IbnxjXcpaf4LLYvaVsuoh+Ikv2dw0hJd2nFhUpd+oQgoxI2zGqV27o
HglqwqvnSnvGljWH0Z7V6CGO9gxfjvnuNIn/HeeAGlebtnivPArZdtQ2kcr8k=</DQ><InverseQ>Ng+HuqhaYN7fNzi/cUF+BLuXNPSInWv7BOdVUEDiGKX+RoRn9eUXjHY
6gk7FqRSkpTeY3/FPyOW//Me4T3SxuM7UbECDjzbIx4CWdSZ4TjmrQRJcn0aPN9t9YYedLQYl8SZZPvv9cfn5cO3utwFwu5dWeh5gkjPI+69OCem/mYE=</InverseQ><D>A
TFIjpbWC0ltn94LOiy7zFLdFfiNBQ++Nnx7RiT7k5fcirQSBEHZsbST5QCOwZITHYxv2GBQMb1WevbX8MDxZz0mYOD0bckhuTkIMObPjAPA+qgQn/DYR05hZ0hqdh74UFxDh
RtXuPWxbZq7vdJVNjo/7v516i86HQ6nbZCUSuogmDRrIyQPyVm02/A4hsYTwBuIJthG8GfK+AIG7pzu4fB8HtEOmqOQJOZ+PZIZQhj60qBFLmDyfJ35E6e70IRZ02azEZGx5
ZaRWO6j30WHSob3IC7NfO5HzI0o6cH8dRScLMh8s0XQ6MS6eo8uCr7tU4BXGM34NX0clsmo2198AQ==</D></RSAKeyValue>";
        private bool Verify1 (byte [] key, byte [] data, byte[] signature)
        {
            SHA384 hash = new SHA384Managed();
            hash.Initialize();
            hash.TransformFinalBlock(data, 0, data.Length);
            byte[] hashval = hash.Hash;
            hash.Dispose();
            ECDsaCng dsa = new ECDsaCng(CngKey.Import(key, CngKeyBlobFormat.EccPrivateBlob));
            dsa.HashAlgorithm = CngAlgorithm.Sha384;
            return dsa.VerifyHash(hashval, signature) ;
        }
        private bool Verify2(byte[] key, byte[] data, byte[] signature)
        {
            SHA256 hash = new SHA256Managed();
            hash.Initialize();
            hash.TransformFinalBlock(data, 0, data.Length);
            byte[] hashval = hash.Hash;
            hash.Dispose();
            ECDsaCng dsa = new ECDsaCng(CngKey.Import(key, CngKeyBlobFormat.EccPrivateBlob));
            dsa.HashAlgorithm = CngAlgorithm.Sha256;
            return dsa.VerifyHash(hashval, signature);
        }
        [Test]

        public void b1Test()
        {
            RunTest1("abcd",BitConverter.ToString(FWkey).Replace("-", ""));
           
        }
        public void RunTest1(string data, string key)
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<Peach>" +
                "   <DataModel name=\"TheDataModel\">" +
                "       <Blob name=\"Signature\">" +
                "           <Fixup class=\"Signature\">" +
                "               <Param name=\"ref\" value=\"Data\"/>" +
                "               <Param name=\"Key\" value=\"{1}\"/>" +
                "               <Param name=\"Hash\" value=\"SHA384\"/>" +
                  "           </Fixup>" +
                "       </Blob>" +
                "       <Blob name=\"Data\" valueType=\"hex\"  value=\"{0}\"/>" +
                "   </DataModel>" +
                "   <StateModel name=\"TheState\" initialState=\"Initial\">" +
                "       <State name=\"Initial\">" +
                "           <Action type=\"output\">" +
                "               <DataModel ref=\"TheDataModel\"/>" +
                "           </Action>" +
                "       </State>" +
                "   </StateModel>" +
                "   <Test name=\"Default\">" +
                "       <StateModel ref=\"TheState\"/>" +
                "       <Publisher class=\"Null\"/>" +
                "   </Test>" +
                "</Peach>";
            xml = string.Format(xml, data, key);
            PitParser parser = new PitParser();

            Dom.Dom dom = parser.asParser(null, new MemoryStream(ASCIIEncoding.ASCII.GetBytes(xml)));

            RunConfiguration config = new RunConfiguration();
            config.singleIteration = true;

            Engine e = new Engine(null);
            e.startFuzzing(dom, config);

            // verify values
            Assert.AreEqual(1, values.Count);
            byte [] signature = values[0].ToArray();
            Assert.IsTrue(Verify1(FWkey, Enumerable.Range(0, data.Length / 2).Select(x => Convert.ToByte(data.Substring(x * 2, 2), 16)).ToArray(), signature));
        }

    }
}
