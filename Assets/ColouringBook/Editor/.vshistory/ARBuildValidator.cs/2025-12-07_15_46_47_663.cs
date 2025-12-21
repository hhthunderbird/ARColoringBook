using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

namespace Felina.ARColoringBook.Editor
{
    public class ARBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild( BuildReport report )
        {
            Debug.Log( "[Felina] Validating Scene Components..." );

            // 1. Validate Scanner Manager
            ARScannerManager scanner = Object.FindAnyObjectByType<ARScannerManager>();
            if ( scanner != null )
            {
                if ( scanner.unwarpMaterial == null )
                    ThrowError( "ARScannerManager is missing the 'Unwarp Material'. Please assign it.", scanner );

                // Note: We check the bridge via the serialized field since the interface might not be active in Editor
                SerializedObject so = new SerializedObject( scanner );
                if ( so.FindProperty( "arBridgeComponent" ).objectReferenceValue == null )
                    ThrowError( "ARScannerManager is missing the 'AR Bridge Component'.", scanner );
            }
            else
            {
                // Warn but don't fail (maybe they are building a menu scene)
                Debug.LogWarning( "[Felina Warning] No ARScannerManager found in the open scene. If this is an AR scene, the app will not work." );
            }

            // 2. Validate Content Spawner
            ARContentSpawner spawner = Object.FindAnyObjectByType<ARContentSpawner>();
            if ( spawner != null )
            {
                if ( spawner.referenceLibrary == null )
                    ThrowError( "ARContentSpawner is missing the 'Reference Library'.", spawner );

                // Check Mappings
                XRReferenceImageLibrary lib = spawner.referenceLibrary;
                HashSet<string> validNames = new HashSet<string>();
                for ( int i = 0; i < lib.count; i++ ) validNames.Add( lib[ i ].name );

                for ( int i = 0; i < spawner.contentLibrary.Count; i++ )
                {
                    var entry = spawner.contentLibrary[ i ];

                    // Check A: Is Prefab missing?
                    if ( entry.prefab == null )
                        ThrowError( $"ARContentSpawner Mapping #{i + 1} ('{entry.imageName}') has no Prefab assigned!", spawner );

                    // Check B: Does the name match the library? (Catches typo errors)
                    if ( !validNames.Contains( entry.imageName ) )
                        ThrowError( $"ARContentSpawner Mapping #{i + 1} uses name '{entry.imageName}', but that name does not exist in the assigned Library '{lib.name}'.", spawner );
                }
            }

            // 3. Validate Paintable Objects (Prefabs)
            // We check objects in the scene AND prefabs referenced by the spawner
            List<ARPaintableObject> paintablesToCheck = new List<ARPaintableObject>();
            paintablesToCheck.AddRange( Object.FindObjectsByType<ARPaintableObject>( FindObjectsSortMode.None ) );

            if ( spawner != null )
            {
                foreach ( var mapping in spawner.contentLibrary )
                {
                    if ( mapping.prefab != null )
                    {
                        var p = mapping.prefab.GetComponentInChildren<ARPaintableObject>();
                        if ( p != null ) paintablesToCheck.Add( p );
                    }
                }
            }

            foreach ( var paintable in paintablesToCheck )
            {
                // We use SerializedObject to check the hidden 'referenceImageName' field
                SerializedObject so = new SerializedObject( paintable );
                string refName = so.FindProperty( "referenceImageName" ).stringValue;

                if ( string.IsNullOrEmpty( refName ) )
                {
                    ThrowError( $"The ARPaintableObject on '{paintable.name}' has no Image Name selected.", paintable.gameObject );
                }
            }

            Debug.Log( "[Felina] Component Validation Passed." );
        }

        private void ThrowError( string msg, Object context )
        {
            // Highlight the object in the Hierarchy/Project
            EditorGUIUtility.PingObject( context );
            Selection.activeObject = context;

            // Stop Build
            throw new BuildFailedException( $"[Felina Build Error] {msg}" );
        }
    }
}