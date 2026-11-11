// Optional addon assets never replace the original signed recovery pair.
using System;
using System.IO;
using System.Security.Cryptography;

public static class BootResourceSelection
{
 public static bool Stage(string resources,string work,string package,string optionalRoot,Action<string> diagnostic=null)
 {
  string archive=package+".tar.gz",signature=archive+".signature";
  try {
   if(File.Exists(Path.Combine(optionalRoot,"enabled"))){
    string version=File.ReadAllText(Path.Combine(optionalRoot,"boot-resource-version")).Trim();
    if(version.Length!=64||Convert.FromHexString(version).Length!=32)throw new InvalidDataException();
    string extra=Path.Combine(optionalRoot,"boot-resources",version);
    var info=new FileInfo(Path.Combine(extra,archive));var sigInfo=new FileInfo(Path.Combine(extra,signature));
    if(info.Length<1||info.Length>65536||sigInfo.Length<1||sigInfo.Length>4096)throw new InvalidDataException();
    byte[] bytes=File.ReadAllBytes(info.FullName),signed=File.ReadAllBytes(sigInfo.FullName);
    if(!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),version,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException();
    using(var rsa=RSA.Create()){
     rsa.ImportFromPem(File.ReadAllText(Path.Combine(resources,"public.pem")));
     if(!rsa.VerifyData(bytes,signed,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new InvalidDataException();
    }
    File.WriteAllBytes(Path.Combine(work,archive),bytes);
    File.WriteAllBytes(Path.Combine(work,signature),signed);
    return true;
   }else diagnostic?.Invoke("optional marker unavailable");
  }catch(Exception e) { diagnostic?.Invoke("optional resource fallback:"+e.GetType().Name); }
  File.Copy(Path.Combine(resources,archive),Path.Combine(work,archive),true);
  File.Copy(Path.Combine(resources,signature),Path.Combine(work,signature),true);
  return false;
 }
}
