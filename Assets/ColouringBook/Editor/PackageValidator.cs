using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

namespace Felina.ARColoringBook.Editor
{
    /// <summary>
    /// Validates package installation and provides guidance for multi-version Unity support
    /// </summary>
    [InitializeOnLoad]
    public static class PackageValidator
    {
        private const string PACKAGE_NAME = "Felina AR Coloring Book";
        private const string MENU_PATH = "Felina/";
        
        static PackageValidator()
        {
            EditorApplication.delayCall += ValidateOnStartup;
        }

        private static void ValidateOnStartup()
        {
            if (SessionState.GetBool("FelinaValidated", false))
                return;
                
            SessionState.SetBool("FelinaValidated", true);
            
            ValidatePackageSetup(false);
        }

        [MenuItem(MENU_PATH + "Validate Package Setup")]
        public static void ValidatePackageSetupMenu()
        {
            ValidatePackageSetup(true);
        }

        [MenuItem(MENU_PATH + "Installation Guide")]
        public static void OpenInstallationGuide()
        {
            var path = "Assets/ColouringBook/INSTALLATION.md";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
            else
            {
                EditorUtility.DisplayDialog("Installation Guide", 
                    "Installation guide not found. Please ensure package is correctly installed.", "OK");
            }
        }

        private static void ValidatePackageSetup(bool showSuccessDialog)
        {
            bool hasErrors = false;
            bool hasWarnings = false;
            var messages = new System.Text.StringBuilder();
            
            messages.AppendLine($"=== {PACKAGE_NAME} Validation ===\n");
            
            var unityVersion = Application.unityVersion;
            messages.AppendLine($"Unity Version: {unityVersion}");
            
            var version = new Version(Application.unityVersion.Split('.')[0] + "." + 
                                     Application.unityVersion.Split('.')[1]);
            
            bool hasARFoundation = HasPackage("com.unity.xr.arfoundation");
            if (hasARFoundation)
            {
                var arVersion = GetPackageVersion("com.unity.xr.arfoundation");
                messages.AppendLine($"? AR Foundation: {arVersion}");
                
                if (version.Major == 2019 && !arVersion.StartsWith("2."))
                {
                    hasWarnings = true;
                    messages.AppendLine($"? Unity 2019.4 should use AR Foundation 2.x, found {arVersion}");
                }
                else if (version.Major == 2020 && !arVersion.StartsWith("4."))
                {
                    hasWarnings = true;
                    messages.AppendLine($"? Unity 2020.x should use AR Foundation 4.x, found {arVersion}");
                }
                else if (version.Major == 2021 && !(arVersion.StartsWith("4.") || arVersion.StartsWith("5.")))
                {
                    hasWarnings = true;
                    messages.AppendLine($"? Unity 2021.x should use AR Foundation 4.x-5.x, found {arVersion}");
                }
                else if (version.Major >= 2023 && !arVersion.StartsWith("6."))
                {
                    hasWarnings = true;
                    messages.AppendLine($"? Unity 2023+/6.x should use AR Foundation 6.x, found {arVersion}");
                }
            }
            else
            {
                hasErrors = true;
                messages.AppendLine("? AR Foundation: NOT INSTALLED");
                messages.AppendLine("  ? Install via Window > Package Manager");
            }
            
            bool hasMathematics = HasPackage("com.unity.mathematics");
            if (hasMathematics)
            {
                var mathVersion = GetPackageVersion("com.unity.mathematics");
                messages.AppendLine($"? Unity Mathematics: {mathVersion}");
            }
            else
            {
                hasErrors = true;
                messages.AppendLine("? Unity Mathematics: NOT INSTALLED");
                messages.AppendLine("  ? Install via Window > Package Manager");
            }
            
            bool hasARKit = HasPackage("com.unity.xr.arkit");
            bool hasARCore = HasPackage("com.unity.xr.arcore");
            
            messages.AppendLine($"\nPlatform Plugins:");
            if (hasARKit)
                messages.AppendLine($"? ARKit XR Plugin: {GetPackageVersion("com.unity.xr.arkit")}");
            else
                messages.AppendLine("? ARKit XR Plugin: Not installed (required for iOS)");
                
            if (hasARCore)
                messages.AppendLine($"? ARCore XR Plugin: {GetPackageVersion("com.unity.xr.arcore")}");
            else
                messages.AppendLine("? ARCore XR Plugin: Not installed (required for Android)");
            
            bool hasUniTask = System.Type.GetType("Cysharp.Threading.Tasks.UniTask") != null;
            if (hasUniTask)
            {
                messages.AppendLine($"? UniTask: Installed");
            }
            else
            {
                hasErrors = true;
                messages.AppendLine("? UniTask: NOT FOUND");
                messages.AppendLine("  ? Package includes UniTask, ensure it's imported");
            }
            
            messages.AppendLine($"\nXR Plug-in Management:");
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            messages.AppendLine($"Current Platform: {buildTarget}");
            
            if (buildTarget == BuildTarget.iOS && !hasARKit)
            {
                hasWarnings = true;
                messages.AppendLine("? iOS platform but ARKit not installed");
            }
            if (buildTarget == BuildTarget.Android && !hasARCore)
            {
                hasWarnings = true;
                messages.AppendLine("? Android platform but ARCore not installed");
            }
            
            messages.AppendLine($"\n=== Recommended Versions for Unity {unityVersion} ===");
            
            if (version.Major == 2019)
            {
                messages.AppendLine("• AR Foundation: 2.1.18");
                messages.AppendLine("• ARKit XR Plugin: 2.1.18");
                messages.AppendLine("• ARCore XR Plugin: 2.1.23");
                messages.AppendLine("• Mathematics: 1.2.1+");
            }
            else if (version.Major == 2020)
            {
                messages.AppendLine("• AR Foundation: 4.1.13");
                messages.AppendLine("• ARKit/ARCore: 4.1.x");
                messages.AppendLine("• Mathematics: 1.2.1+");
            }
            else if (version.Major == 2021)
            {
                messages.AppendLine("• AR Foundation: 4.2.10 or 5.1.5");
                messages.AppendLine("• ARKit/ARCore: matching version");
                messages.AppendLine("• Mathematics: 1.2.1+");
            }
            else if (version.Major == 2022)
            {
                messages.AppendLine("• AR Foundation: 5.1.5");
                messages.AppendLine("• ARKit/ARCore: 5.1.x");
                messages.AppendLine("• Mathematics: 1.3.x");
            }
            else
            {
                messages.AppendLine("• AR Foundation: 6.3.1");
                messages.AppendLine("• ARKit/ARCore: 6.3.x");
                messages.AppendLine("• Mathematics: 1.3.x");
            }
            
            // 8. Final summary
            messages.AppendLine($"\n=== Validation Summary ===");
            if (hasErrors)
            {
                messages.AppendLine("? ERRORS FOUND - Package will not work correctly");
                messages.AppendLine("? See INSTALLATION.md for setup instructions");
            }
            else if (hasWarnings)
            {
                messages.AppendLine("? WARNINGS - Package may work but versions don't match");
                messages.AppendLine("? Consider updating to recommended versions");
            }
            else
            {
                messages.AppendLine("? ALL CHECKS PASSED - Package is correctly configured");
            }
            
            Debug.Log(messages.ToString());
            
            if (showSuccessDialog || hasErrors)
            {
                if (hasErrors)
                {
                    EditorUtility.DisplayDialog(
                        "Package Validation - Errors Found", 
                        "Package setup has errors. Check Console for details.\n\nSee INSTALLATION.md for setup instructions.",
                        "OK");
                }
                else if (hasWarnings && showSuccessDialog)
                {
                    EditorUtility.DisplayDialog(
                        "Package Validation - Warnings", 
                        "Package setup has warnings. Check Console for details.",
                        "OK");
                }
                else if (showSuccessDialog)
                {
                    EditorUtility.DisplayDialog(
                        "Package Validation - Success", 
                        "All checks passed! Package is correctly configured.",
                        "OK");
                }
            }
        }

        private static bool HasPackage(string packageName)
        {
            var listRequest = UnityEditor.PackageManager.Client.List();
            while (!listRequest.IsCompleted)
                System.Threading.Thread.Sleep(10);
                
            if (listRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                return listRequest.Result.Any(p => p.name == packageName);
            }
            return false;
        }

        private static string GetPackageVersion(string packageName)
        {
            var listRequest = UnityEditor.PackageManager.Client.List();
            while (!listRequest.IsCompleted)
                System.Threading.Thread.Sleep(10);
                
            if (listRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                var package = listRequest.Result.FirstOrDefault(p => p.name == packageName);
                return package?.version ?? "Unknown";
            }
            return "Unknown";
        }
    }
}
