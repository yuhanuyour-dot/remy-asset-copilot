using AssetCopilot;
using System.Text;
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
var root=Path.Combine(Path.GetTempPath(),"remy-install-test-"+Guid.NewGuid().ToString("N"));
try {
    var app=Path.Combine(root,"程序 Program");var data=Path.Combine(root,"模型 Data");
    var dll=Path.Combine(app,"AssetCopilot","dist","AssetCopilot.rhp");Directory.CreateDirectory(Path.GetDirectoryName(dll)!);
    var marker=Path.Combine(app,"remy-install.ini");
    File.WriteAllText(marker,"[Storage]\nDataRoot="+data+"\n",Encoding.Unicode);
    Check(AppPaths.FindInstallRoot(dll)==app,"spaces and Unicode in install root");
    Check(AppPaths.ResolveDataRoot(app,root)==data,"UTF16 data location");
    File.WriteAllText(marker,"[Storage]\nDataRoot=relative\n");bool rejected=false;try{AppPaths.ResolveDataRoot(app,root);}catch(IOException){rejected=true;}Check(rejected,"reject relative configuration");
    File.Delete(marker);
    var local=Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!,"Users","Example","AppData","Local");
    Check(AppPaths.ResolveDataRoot(app,local)==Path.Combine(local,"RemyAssetCopilot","Data"),"fallback on system drive without D dependency");
    Directory.CreateDirectory(Path.Combine(app,"runtime"));File.WriteAllText(Path.Combine(app,"runtime","node.exe"),"");
    Check(AppPaths.FindInstallRoot(dll)==app&&AppPaths.ResolveDataRoot(app,local)==app,"legacy portable migration");
    Console.WriteLine(checks+" path checks passed");
} finally { if(Directory.Exists(root))Directory.Delete(root,true); }
