using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.iOS.Xcode.Custom;
using UnityEditor.Callbacks;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System;

public class XcodeSettingsPostProcesser
{
    static string XcodePath = "";
    [PostProcessBuildAttribute (0)]

    public static void OnPostprocessBuild(BuildTarget BuildTarget, string path)
    {
        if (BuildTarget == BuildTarget.iOS)
        {
            XcodePath = path;
            string projPath = PBXProject.GetPBXProjectPath(path);
            PBXProject proj = new PBXProject();
            proj.ReadFromString(File.ReadAllText(projPath));

            //GenerateProjFile(proj, path);//处理Xcode工程

            string plistPath = path + "/Info.plist";
            PlistDocument plist = new PlistDocument();
            plist.ReadFromString(File.ReadAllText(plistPath));

            generatePlistFile(plist.root, path);//处理plist文件


            //File.WriteAllText(projPath, proj.WriteToString());
            File.WriteAllText(plistPath, plist.WriteToString());

            //ParseIcon();//处理图标
            ParseSplashXLib(new Color(34 / 255f, 44 / 255f, 55 / 255f));//修改背景色
            //this.Buildipa();//代码请见Part2博客
            //generateFBSettingsConfig();// FaceBook SDK
            //generateHelpShiftConfig();//Helpshift SDK
        }
    }

    private static void GenerateProjFile(PBXProject proj, string path)
    {
        string target = proj.TargetGuidByName(PBXProject.GetUnityTargetName());

        var codesign = Debug.isDebugBuild ? "iPhone Developer: xxxxxxxxxxx" : "iPhone Distribution: xxxxxxxx";
        var provision = Debug.isDebugBuild ? "xxxxx1" : "xxxxx2";

        proj.SetBuildProperty(target, "CODE_SIGN_IDENTITY", codesign);
        proj.SetBuildProperty(target, "PROVISIONING_PROFILE_SPECIFIER", provision);
        proj.SetBuildProperty(target, "CODE_SIGN_ENTITLEMENTS", "KeychainAccessGroups.plist");
        proj.SetBuildProperty(target, "DEVELOPMENT_TEAM", "xxxxxxxxxx");

        proj.SetBuildProperty(target, "ENABLE_BITCODE", "NO");
        proj.SetSystemCapabilities(target, "com.apple.Push", "1");
        proj.SetSystemCapabilities(target, "com.apple.GameCenter", "1");
        proj.SetSystemCapabilities(target, "com.apple.InAppPurchase", "1");
        proj.RemoveFilesByProjectPathRecursive("Libraries/Plugins/Android"); //移除某个目录,根据开发者需求

        proj.AddBuildProperty(target, "HEADER_SEARCH_PATHS", Application.dataPath + "/_PlatformAssets/Platforms/xxxxxxxx");//修改Xcode索引目录

        //keychain
        proj.AddFile(Application.dataPath + "你的目录/KeychainAccessGroups.plist", "KeychainAccessGroups.plist");
        proj.SetBuildProperty(target, "CODE_SIGN_ENTITLEMENTS", Application.dataPath + "你的目录/KeychainAccessGroups.plist");

        //weixin framework
        //proj.AddFrameworkToProject (target, "SystemConfiguration.framework", false);

        //SvUDIDTools是UDID文件，可以忽略。
        //AddFile添加文件到Xcode目录，返回文件GUID
        // AddFileToBuild功能是将GUID文件添加到Xcode BuildPhases阶段
        var fileGUID = "";
        fileGUID = proj.AddFile(Application.dataPath + "你的目录/SvUDIDTools.h", "Libraries/Plugins/IOS/SvUDIDTool/SvUDIDTools.h");
        proj.AddFileToBuild(target, fileGUID);
        fileGUID = proj.AddFile(Application.dataPath + "你的目录/SvUDIDTools.m", "Libraries/Plugins/IOS/SvUDIDTool/SvUDIDTools.m");
        proj.AddFileToBuild(target, fileGUID);//添加到Xcode BuildPhases阶段

        //todo 多语言支持后续完善
        //localizable  自动添加Xcode语言文件
        //var infoDirs = Directory.GetDirectories(Application.dataPath + "你的目录/lang/infoplist/");
        //for (var i = 0; i < infoDirs.Length; ++i)
        //{
        //    var files = Directory.GetFiles(infoDirs[i], "*.strings");

        //proj.AddLocalization(files[0], "InfoPlist.strings", "InfoPlist.strings");
        //}

        //var localdirs = Directory.GetDirectories(Application.dataPath + "你的目录/lang/localizable/");
        //for (var i = 0; i < localdirs.Length; ++i)
        //{
        //    var files = Directory.GetFiles(localdirs[i], "*.strings");
        //    proj.AddLocalization(files[0], "Localizable.strings", "Localizable.strings");
        //}
        fileGUID = proj.AddFile(Application.dataPath + "/Plugins/IOS/notificationsound.caf", "notificationsound.caf"); //添加推送音效
        proj.AddFileToBuild(target, fileGUID);
    }

    private static void generatePlistFile(PlistElementDict rootDict, string path)
    {
        rootDict.SetString("CFBundleIdentifier", "com.gekko.rok");
        rootDict.SetString("CFBundleDisplayName", "Rage of Kings");
        //rootDict.SetString("CFBundleVersion", GetVer());
        rootDict.SetString("NSPhotoLibraryUsageDescription", "Use Photo");
        rootDict.SetString("NSCameraUsageDescription", "Use Camera");
        //rootDict.SetString("CFBundleShortVersionString", GKVersion.GAME_VERSION);
        rootDict.SetString("ITSAppUsesNonExemptEncryption", "false");
        rootDict.SetString("LSHasLocalizedDisplayName", "true");

        //weixin scheme
        PlistElementArray urlArray = null;
        if (!rootDict.values.ContainsKey("CFBundleURLTypes"))
        {
            urlArray = rootDict.CreateArray("CFBundleURLTypes");
        }
        else
        {
            urlArray = rootDict.values["CFBundleURLTypes"].AsArray();
        }
        var urlTypeDict = urlArray.AddDict();
        urlTypeDict.SetString("CFBundleURLName", "weixin");
        var urlScheme = urlTypeDict.CreateArray("CFBundleURLSchemes");
        urlScheme.AddString("weixin_id");

        if (!rootDict.values.ContainsKey("LSApplicationQueriesSchemes"))
        {
            urlArray = rootDict.CreateArray("LSApplicationQueriesSchemes");
        }
        else
        {
            urlArray = rootDict["LSApplicationQueriesSchemes"].AsArray();
        }
        urlArray.AddString("weixin");

        //Gamecenter
        if (rootDict.values.ContainsKey("UIRequiredDeviceCapabilities"))
        {
            rootDict.values.Remove("UIRequiredDeviceCapabilities");
        }
        var arr = rootDict.CreateArray("UIRequiredDeviceCapabilities");
        arr.AddString("armv7");
        arr.AddString("gamekit");
    }

    protected static void ParseIcon()
    {
        string sourcePath = "/_ExportTextures/appIcon/{0}.png";
        var icoList = new List<string>() {
        "Icon",
        "Icon-72",
        "Icon-76",
        "Icon-120",
        "Icon@2x",
        "Icon-144",
        "Icon-152",
        "Icon-167",
        "Icon-180",
        "Icon-Store"//1024*1024 , xcode9+
        };
        foreach (var ico in icoList)
        {
            File.Copy(Application.dataPath + string.Format(sourcePath, ico), XcodePath + string.Format("/Unity-iPhone/Images.xcassets/AppIcon.appiconset/{0}.png", ico), true);
        }
    }

    //using System.Xml;
    protected static void ParseSplashXLib(Color color)
    {
        Action<string> ModifyXML = (xmlPath) => {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlPath);
            XmlNodeList nodelist = xmlDoc.SelectSingleNode("document").ChildNodes;
            foreach (XmlElement item in nodelist)
            {
                if (item.Name == "objects")
                {
                    foreach (XmlElement item1 in item.ChildNodes)
                    {
                        if (item1.Name == "view")
                        {
                            foreach (XmlElement item2 in item1.ChildNodes)
                            {
                                if (item2.Name == "color")
                                {
                                    item2.SetAttribute("red", color.r.ToString());
                                    item2.SetAttribute("green", color.g.ToString());
                                    item2.SetAttribute("blue", color.b.ToString());
                                    item2.SetAttribute("alpha", color.a.ToString());
                                }
                            }
                        }
                    }
                }
            }
            xmlDoc.Save(xmlPath);
        };

        ModifyXML(XcodePath + "/LaunchScreen-iPhone.xib");
        ModifyXML(XcodePath + "/LaunchScreen-iPad.xib");
    }

}