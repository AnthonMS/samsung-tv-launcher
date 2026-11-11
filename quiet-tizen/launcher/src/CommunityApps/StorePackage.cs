using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace QuietCommunity;

public sealed class SigningProfile
{
 public string Id {get;set;}="";
 public string Name {get;set;}="";
 public string AuthorFile {get;set;}="";
 public string DistributorFile {get;set;}="";
 public string AuthorPassword {get;set;}="";
 public string DistributorPassword {get;set;}="";
 public string Level {get;set;}="public";
 public string AuthorFingerprint(string directory){using var cert=Author(directory);return Convert.ToHexString(SHA256.HashData(cert.RawData)).ToLowerInvariant();}
 static string Within(string root,string path){var full=Path.GetFullPath(Path.Combine(root,path));if(!full.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.Ordinal)||Path.IsPathRooted(path))throw new InvalidDataException("Invalid signing profile path.");return full;}
 public X509Certificate2 Author(string root)=>new(Within(root,AuthorFile),AuthorPassword,X509KeyStorageFlags.EphemeralKeySet);
 public X509Certificate2 Distributor(string root)=>new(Within(root,DistributorFile),DistributorPassword,X509KeyStorageFlags.EphemeralKeySet);
 public X509Certificate2Collection Chain(string root,bool author){var c=new X509Certificate2Collection();c.Import(Within(root,author?AuthorFile:DistributorFile),author?AuthorPassword:DistributorPassword,X509KeyStorageFlags.EphemeralKeySet);return c;}
 public static List<SigningProfile> Load(string directory){var path=Path.Combine(directory,"profiles.json");return File.Exists(path)?JsonSerializer.Deserialize<List<SigningProfile>>(File.ReadAllText(path))??new():new();}
 public void Validate(string directory,bool partner)
 {
  if(partner&&Level!="partner")throw new InvalidOperationException("This app requires a Partner signing profile.");
  using var a=Author(directory);using var d=Distributor(directory);var now=DateTime.UtcNow;
  foreach(var c in new[]{a,d})if(!c.HasPrivateKey||c.NotBefore.ToUniversalTime()>now||c.NotAfter.ToUniversalTime()<now)throw new InvalidOperationException("Signing profile is expired, not valid yet, or has no private key.");
 }
}
public sealed class PackageInfo
{
 public string Id,AppId,Name,Version,RequiredVersion;
 public bool Partner;
}
public static class StorePackage
{
 [DllImport("libc.so.6")]static extern uint getuid();
 [DllImport("libpkgmgr-info.so.0")]static extern int pkgmgrinfo_pkginfo_create_certinfo(out IntPtr handle);
 [DllImport("libpkgmgr-info.so.0")]static extern int pkgmgrinfo_pkginfo_load_certinfo(string id,IntPtr handle,int uid);
 [DllImport("libpkgmgr-info.so.0")]static extern int pkgmgrinfo_pkginfo_get_cert_value(IntPtr handle,int type,out IntPtr value);
 [DllImport("libpkgmgr-info.so.0")]static extern int pkgmgrinfo_pkginfo_destroy_certinfo(IntPtr handle);
 public static PackageInfo Inspect(string file)
 {
  using var zip=ZipFile.OpenRead(file);long total=0;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  if(zip.Entries.Count>15000)throw new InvalidDataException("Package has too many files.");
  foreach(var entry in zip.Entries){var n=entry.FullName;int kind=(entry.ExternalAttributes>>16)&0xf000;if((kind!=0&&kind!=0x8000&&kind!=0x4000)||n.Contains('\\')||n.StartsWith('/')||n.Contains(':')||n.Split('/').Any(x=>x==".."||x==".")||!names.Add(n)||entry.Length>StorePolicy.MaxExpandedBytes||(total+=entry.Length)>StorePolicy.MaxExpandedBytes)throw new InvalidDataException("Package has unsafe paths, duplicate entries or excessive expanded size.");}
  // Validate actual inflated bytes too; do not trust attacker-supplied ZIP size metadata.
  long inflated=0;var buffer=new byte[65536];foreach(var entry in zip.Entries){using var stream=entry.Open();long length=0;int got;while((got=stream.Read(buffer,0,buffer.Length))>0){length+=got;inflated+=got;if(length>entry.Length||inflated>StorePolicy.MaxExpandedBytes)throw new InvalidDataException("Expanded package exceeds its supported size.");}if(length!=entry.Length)throw new InvalidDataException("Package entry length is invalid.");}
  var web=zip.GetEntry("config.xml");var native=zip.GetEntry("tizen-manifest.xml");if((web==null)==(native==null))throw new InvalidDataException("Package must have exactly one Tizen manifest.");
  if((web!=null)!=Path.GetExtension(file).Equals(".wgt",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Package extension does not match its manifest.");
  var target=web??native;if(target.Length>1024*1024)throw new InvalidDataException("Package manifest is too large.");
  using var input=target.Open();using var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024});var xml=XDocument.Load(reader);var root=xml.Root;
  string attr(XElement e,string name)=>e?.Attribute(name)?.Value??"";
  var app=web!=null?root.Elements().FirstOrDefault(x=>x.Name.LocalName=="application"):root.Elements().FirstOrDefault(x=>x.Name.LocalName=="ui-application");
  var info=new PackageInfo{Id=web!=null?attr(app,"package"):attr(root,"package"),AppId=web!=null?attr(app,"id"):attr(app,"appid"),Name=root.Elements().FirstOrDefault(x=>x.Name.LocalName=="name")?.Value??app?.Elements().FirstOrDefault(x=>x.Name.LocalName=="label")?.Value??"",Version=attr(root,"version"),RequiredVersion=attr(web!=null?app:root,"required_version")};
  if(info.RequiredVersion=="")info.RequiredVersion=attr(root,"api-version");
  if(!StorePolicy.ValidId(info.Id)||!StorePolicy.ValidId(info.AppId)||!System.Version.TryParse(info.Version,out _))throw new InvalidDataException("Package identity or version is invalid; only launchable apps are supported.");
  if(StorePolicy.Protected(info.Id)||StorePolicy.Protected(info.AppId))throw new InvalidOperationException("This package is part of the managed TV setup.");
  var architecture=attr(root,"architecture");if(architecture!=""&&architecture!="arm"&&architecture!="armv7l"&&architecture!="all")throw new InvalidOperationException("Native package is not an ARMv7 TV build.");
  var privileges=root.Descendants().Where(x=>x.Name.LocalName=="privilege").Select(x=>attr(x,"name")).ToArray();
  info.Partner=privileges.Any(x=>x.Contains("partner")||x.EndsWith("/application.kill")||x.EndsWith("/tvinputdevice.admin"));
  return info;
 }
 public static string InstalledAuthor(string packageId)
 {
  if(!StorePolicy.ValidId(packageId))return "";
  foreach(var suffix in new[]{"/res/wgt/author-signature.xml","/author-signature.xml"})try{
   var path="/opt/usr/apps/"+packageId+suffix;if(!File.Exists(path))continue;
   using var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024});var doc=XDocument.Load(reader);
   var value=doc.Descendants().First(x=>x.Name.LocalName=="X509Certificate").Value;
   using var cert=new X509Certificate2(Convert.FromBase64String(value));return Convert.ToHexString(SHA256.HashData(cert.RawData)).ToLowerInvariant();
  }catch(IOException){}catch(UnauthorizedAccessException){}catch(XmlException){}catch(CryptographicException){}catch(InvalidOperationException){}catch(FormatException){}
  // Query the package-manager metadata when another package's read-only files are isolated.
  IntPtr handle=IntPtr.Zero;
  try{if(pkgmgrinfo_pkginfo_create_certinfo(out handle)==0&&handle!=IntPtr.Zero&&pkgmgrinfo_pkginfo_load_certinfo(packageId,handle,(int)getuid())==0&&pkgmgrinfo_pkginfo_get_cert_value(handle,2,out var value)==0&&value!=IntPtr.Zero){var text=Marshal.PtrToStringUTF8(value);using var cert=text.Contains("-----BEGIN CERTIFICATE-----")?X509Certificate2.CreateFromPem(text):new X509Certificate2(Convert.FromBase64String(text));return Convert.ToHexString(SHA256.HashData(cert.RawData)).ToLowerInvariant();}}
  catch(DllNotFoundException){}catch(EntryPointNotFoundException){}catch(CryptographicException){}catch(FormatException){}
  finally{if(handle!=IntPtr.Zero)try{pkgmgrinfo_pkginfo_destroy_certinfo(handle);}catch{}}
  return "";
 }
 [DllImport("libc.so.6")]static extern int chmod(string path,uint mode);
 public static void Restrict(string path){if(OperatingSystem.IsLinux()&&chmod(path,384)!=0)throw new IOException("Could not restrict private signing material.");}
}
