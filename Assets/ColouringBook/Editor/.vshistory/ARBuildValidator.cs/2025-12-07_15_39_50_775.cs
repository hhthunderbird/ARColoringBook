using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using static UnityEngine.EventSystems.EventTrigger;

namespace Felina.ARColoringBook.Editor
{
    public class ARBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild( BuildReport report )
        {
            Debug.Log( "[Felina] Validating AR Reference Libraries..." );

            // Find all libraries
            string[] guids = AssetDatabase.FindAssets( "t:XRReferenceImageLibrary" );

            foreach ( string guid in guids )
            {
                string path = AssetDatabase.GUIDToAssetPath( guid );
                XRReferenceImageLibrary lib = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>( path );

                if ( lib == null ) continue;

                // Use the public API on XRReferenceImageLibrary to read image entries reliably
                var enumerator = lib.GetEnumerator();
                int i = -1;
                while ( enumerator.MoveNext() )
                {
                    i++;
                    var item = enumerator.Current;

                    // 1. Check Name
                    if ( string.IsNullOrEmpty( item.name ) )
                    {
                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry {i} has an EMPTY Name!";
                        EditorGUIUtility.PingObject( lib );
                        throw new BuildFailedException( errorMsg );
                    }

                    // 2. Check Texture
                    if ( item.texture == null )
                    {
                        // Extra diagnostic logging to help identify why texture appears null
                        Debug.LogError($"[Felina] Missing texture for library '{lib.name}', entry #{i} ('{item.name}'). Attempting to dump serialized properties...");

                        try
                        {
                            var so = new SerializedObject(lib);
                            so.Update();
                            var imagesProp = so.FindProperty("m_Images");
                            if ( imagesProp != null && i < imagesProp.arraySize )
                            {
                                var entryProp = imagesProp.GetArrayElementAtIndex(i);
                                var iter = entryProp.Copy();
                                bool enterChildren = true;
                                // Iterate visible children and log their property paths and types
                                for ( bool hasNext = iter.NextVisible(enterChildren); hasNext; hasNext = iter.NextVisible(false) )
                                {
                                    enterChildren = false;
                                    string path = iter.propertyPath;
                                    string type = iter.propertyType.ToString();
                                    string val = "";
                                    if ( iter.propertyType == SerializedPropertyType.ObjectReference )
                                    {
                                        val = iter.objectReferenceValue != null ? iter.objectReferenceValue.name : "null";
                                    }
                                    else if ( iter.propertyType == SerializedPropertyType.String )
                                    {
                                        val = iter.stringValue;
                                    }
                                    else if ( iter.propertyType == SerializedPropertyType.Vector2 )
                                    {
                                        val = iter.vector2Value.ToString();
                                    }
                                    Debug.Log($"[Felina] Serialized Prop: {path} ({type}) = {val}");
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[Felina] Could not find 'm_Images' or index out of range when dumping serialized data.");
                            }
                        }
                        catch ( System.Exception ex )
                        {
                            Debug.LogWarning($"[Felina] Exception while dumping serialized data: {ex.Message}");
                        }

                        string errorMsg = $"[Felina Build Error] In Library '{lib.name}', Entry '{item.name}' has no Texture assigned!";
                        EditorGUIUtility.PingObject( lib );
                        throw new BuildFailedException( errorMsg );
                    }

                    // 3. Optional Warning: Zero Size
                    if ( item.specifySize && item.size == Vector2.zero )
                    {
                        Debug.LogWarning( $"[Felina Warning] In Library '{lib.name}', Entry '{item.name}' has Physical Size set to Zero." );
                    }
                }
            }

            Debug.Log( "[Felina] Validation Passed." );
        }
    }
}