using System.Security.Cryptography;
using System.Text;
class BootResourcesTests
{
 static int checks;
 static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
 static void Main(){
  string root=Path.Combine(Path.GetTempPath(),"qt-boot-resources-"+Guid.NewGuid().ToString("N"));
  string original=Path.Combine(root,"original"),work=Path.Combine(root,"work"),extra=Path.Combine(root,"addon");
  Directory.CreateDirectory(original);Directory.CreateDirectory(work);Directory.CreateDirectory(extra);
  try{
   using var rsa=RSA.Create(2048);string name="example-package",archive=name+".tar.gz",signature=archive+".signature";
   File.WriteAllText(Path.Combine(original,"public.pem"),"-----BEGIN PUBLIC KEY-----\n"+Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(),Base64FormattingOptions.InsertLineBreaks)+"\n-----END PUBLIC KEY-----\n");
   byte[] old=Encoding.UTF8.GetBytes("original payload"),next=Encoding.UTF8.GetBytes("new payload");
   File.WriteAllBytes(Path.Combine(original,archive),old);File.WriteAllBytes(Path.Combine(original,signature),rsa.SignData(old,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
   Check(!BootResourceSelection.Stage(original,work,name,extra)&&File.ReadAllBytes(Path.Combine(work,archive)).SequenceEqual(old),"missing addon preserves original");
   File.WriteAllText(Path.Combine(extra,"enabled"),"");
   string version=Convert.ToHexString(SHA256.HashData(next)).ToLowerInvariant();string assets=Path.Combine(extra,"boot-resources",version);Directory.CreateDirectory(assets);
   File.WriteAllText(Path.Combine(extra,"boot-resource-version"),version);File.WriteAllBytes(Path.Combine(assets,archive),next);
   byte[] sig=rsa.SignData(next,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);File.WriteAllBytes(Path.Combine(assets,signature),sig);
   Check(BootResourceSelection.Stage(original,work,name,extra)&&File.ReadAllBytes(Path.Combine(work,archive)).SequenceEqual(next),"complete trusted version selected");
   File.Delete(Path.Combine(assets,signature));
   Check(!BootResourceSelection.Stage(original,work,name,extra)&&File.ReadAllBytes(Path.Combine(work,archive)).SequenceEqual(old),"half pair falls back to complete original");
   File.WriteAllBytes(Path.Combine(assets,signature),sig);File.WriteAllBytes(Path.Combine(assets,archive),old);
   Check(!BootResourceSelection.Stage(original,work,name,extra),"mismatched version digest rejected");
   File.WriteAllBytes(Path.Combine(assets,archive),next);File.WriteAllBytes(Path.Combine(assets,signature),new byte[256]);
   Check(!BootResourceSelection.Stage(original,work,name,extra),"invalid optional signature rejected");
   File.WriteAllText(Path.Combine(extra,"boot-resource-version"),"../outside");
   Check(!BootResourceSelection.Stage(original,work,name,extra),"version cannot escape addon directory");
   File.WriteAllText(Path.Combine(extra,"boot-resource-version"),version);File.WriteAllBytes(Path.Combine(assets,archive),new byte[65537]);
   Check(!BootResourceSelection.Stage(original,work,name,extra),"oversized optional archive rejected");
   Check(rsa.VerifyData(File.ReadAllBytes(Path.Combine(work,archive)),File.ReadAllBytes(Path.Combine(work,signature)),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1),"fallback never leaves mixed signed pair");
   Console.WriteLine("Boot resource tests passed: "+checks+" checks");
  }finally{
   string resolved=Path.GetFullPath(root),parent=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
   if(!string.Equals(Path.GetDirectoryName(resolved),parent,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(resolved).StartsWith("qt-boot-resources-",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected fixture cleanup target");
   Directory.Delete(resolved,true);
  }
 }
}
