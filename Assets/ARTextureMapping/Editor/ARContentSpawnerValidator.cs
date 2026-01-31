using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.ARSubsystems;
using Felina.ARTextureMapping.Runtime;

namespace Felina.ARTextureMapping.Editor
{
    [CustomEditor( typeof( ARContentSpawner ) )]
    public class ARContentSpawnerValidator : UnityEditor.Editor
    {
        private ARContentSpawner _spawner;
        private ARTrackedImageManager _imageManager;
        private XRReferenceImageLibrary _library;

        private List<ValidationResult> _validationResults = new List<ValidationResult>();
        private Vector2 _scrollPosition;

        private class ValidationResult
        {
            public string Name;
            public Vector2 TextureSize;
            public Vector2 PhysicalSize;
            public Vector2 UVBounds;
            public float TextureAspect;
            public float PhysicalAspect;
            public bool IsCompatible;
            public string WarningMessage;
            public MessageType MessageType;
            public GameObject Prefab;
        }

        private void OnEnable()
        {
            _spawner = ( ARContentSpawner ) target;

            _imageManager = _spawner.GetComponent<ARTrackedImageManager>() ?? FindObjectOfType<ARTrackedImageManager>();

            if ( _imageManager != null )
            {
                _library = _imageManager.referenceLibrary as XRReferenceImageLibrary;
            }

            ValidateAll();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space( 10 );
            EditorGUILayout.LabelField( "AR Compatibility Validation", EditorStyles.boldLabel );
            EditorGUILayout.HelpBox(
                "Validates that Physical Size matches Texture Aspect (CRITICAL for tracking stability).",
                MessageType.Info
            );

            EditorGUILayout.BeginHorizontal();
            if ( GUILayout.Button( "Validate All", GUILayout.Height( 30 ) ) )
            {
                ValidateAll();
            }
            EditorGUILayout.EndHorizontal();

            if ( _library == null )
            {
                EditorGUILayout.HelpBox( "No XR Reference Image Library found!", MessageType.Error );
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DisplayValidationSummary();

            if ( _validationResults.Count > 0 )
            {
                EditorGUILayout.Space( 5 );
                DisplayDetailedValidation();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ValidateAll()
        {
            _validationResults.Clear();
            if ( _library == null ) return;

            SerializedObject librarySerialized = new SerializedObject( _library );

            Debug.Log( $"[Validator] Checking {_library.count} images..." );

            for ( int i = 0; i < _library.count; i++ )
            {
                var refImage = _library[ i ];
                var result = ValidateReferenceImage( refImage, i, librarySerialized );
                if ( result != null ) _validationResults.Add( result );
            }
        }

        private ValidationResult ValidateReferenceImage( XRReferenceImage refImage, int index, SerializedObject librarySerialized )
        {
            var result = new ValidationResult
            {
                Name = refImage.name,
                IsCompatible = true,
                MessageType = MessageType.Info
            };

            Texture2D sourceTexture = null;

            if ( librarySerialized != null )
            {
                SerializedProperty imagesProp = librarySerialized.FindProperty( "m_Images" );
                if ( imagesProp != null && index < imagesProp.arraySize )
                {
                    SerializedProperty element = imagesProp.GetArrayElementAtIndex( index );
                    SerializedProperty texProp = element.FindPropertyRelative( "m_Texture" );
                    if ( texProp != null && texProp.objectReferenceValue != null )
                    {
                        sourceTexture = texProp.objectReferenceValue as Texture2D;
                    }
                }
            }

            if ( sourceTexture == null )
            {
                string guidStr = refImage.textureGuid.ToString( "N" );
                if ( guidStr != "00000000000000000000000000000000" )
                {
                    string path = AssetDatabase.GUIDToAssetPath( guidStr );
                    if ( !string.IsNullOrEmpty( path ) )
                        sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>( path );
                }
            }

            if ( sourceTexture == null && !string.IsNullOrEmpty( refImage.name ) )
            {
                string[] guids = AssetDatabase.FindAssets( refImage.name + " t:Texture2D" );
                if ( guids.Length > 0 )
                {
                    string path = AssetDatabase.GUIDToAssetPath( guids[ 0 ] );
                    sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>( path );
                }
            }

            if ( sourceTexture != null )
            {
                result.TextureSize = new Vector2( sourceTexture.width, sourceTexture.height );
                result.TextureAspect = result.TextureSize.x / result.TextureSize.y;
            }
            else
            {
                result.WarningMessage = "Could not find source texture! (Tried Property, GUID, and Name Search)";
                result.MessageType = MessageType.Error;
                result.IsCompatible = false;
                return result;
            }

            if ( refImage.specifySize )
            {
                result.PhysicalSize = refImage.size;
                result.PhysicalAspect = result.PhysicalSize.x / result.PhysicalSize.y;
            }
            else
            {
                result.WarningMessage = "Physical size not specified! Tracking will be Scale-less.";
                result.MessageType = MessageType.Warning;
                result.PhysicalAspect = result.TextureAspect;
            }

            var contentLibProp = serializedObject.FindProperty( "_contentLibrary" );
            GameObject prefab = null;

            if ( contentLibProp != null && contentLibProp.isArray )
            {
                for ( int i = 0; i < contentLibProp.arraySize; i++ )
                {
                    var element = contentLibProp.GetArrayElementAtIndex( i );
                    var nameProperty = element.FindPropertyRelative( "ImageName" );
                    var prefabProperty = element.FindPropertyRelative( "Prefab" );

                    if ( nameProperty != null && prefabProperty != null )
                    {
                        if ( nameProperty.stringValue == refImage.name && prefabProperty.objectReferenceValue != null )
                        {
                            prefab = prefabProperty.objectReferenceValue as GameObject;
                            result.Prefab = prefab;
                            break;
                        }
                    }
                }
            }

            if ( prefab != null )
            {
                var uvBounds = GetUVBounds( prefab );
                if ( uvBounds.HasValue )
                {
                    result.UVBounds = uvBounds.Value;
                }
            }

            CheckCompatibility( result );
            return result;
        }

        private Vector2? GetUVBounds( GameObject prefab )
        {
            if ( prefab == null ) return null;

            var filters = prefab.GetComponentsInChildren<MeshFilter>();
            if ( filters.Length == 0 ) return null;

            MeshFilter bestFilter = null;
            float maxArea = -1f;

            foreach ( var mf in filters )
            {
                if ( mf.sharedMesh == null ) continue;
                Vector3 size = mf.sharedMesh.bounds.size;
                float area = size.x * size.z;
                if ( area > maxArea ) { maxArea = area; bestFilter = mf; }
            }

            if ( bestFilter == null ) return null;

            var mesh = bestFilter.sharedMesh;
            var uvs = mesh.uv;
            if ( uvs == null || uvs.Length == 0 ) return null;

            Vector2 min = new Vector2( float.MaxValue, float.MaxValue );
            Vector2 max = new Vector2( float.MinValue, float.MinValue );

            foreach ( var uv in uvs )
            {
                min = Vector2.Min( min, uv );
                max = Vector2.Max( max, uv );
            }

            return max - min;
        }

        private void CheckCompatibility( ValidationResult result )
        {
            const float AspectTolerance = 0.1f;

            float texturePhysDiff = Mathf.Abs( result.TextureAspect - result.PhysicalAspect );
            float texturePhysRatio = texturePhysDiff / result.TextureAspect;

            var warnings = new List<string>();

            if ( texturePhysRatio > AspectTolerance )
            {
                warnings.Add( $"Texture Aspect ({result.TextureAspect:F2}) != Physical Aspect ({result.PhysicalAspect:F2})." );
                warnings.Add( ">> Fix: Update Physical Size Width/Height to match image proportions." );
                result.IsCompatible = false;
            }

            if ( result.UVBounds != Vector2.zero )
            {
                if ( result.UVBounds.x < 0.9f || result.UVBounds.y < 0.9f )
                {
                    warnings.Add( $"UVs do not fill the texture space ({result.UVBounds.x:P0} x {result.UVBounds.y:P0}). Output may look cropped." );
                }
            }

            if ( !IsPowerOfTwo( ( int ) result.TextureSize.x ) || !IsPowerOfTwo( ( int ) result.TextureSize.y ) )
                warnings.Add( "Texture not Power-of-2 (Performance warning)" );

            if ( warnings.Count > 0 )
            {
                result.WarningMessage = string.Join( "\n", warnings );
                result.MessageType = result.IsCompatible ? MessageType.Warning : MessageType.Error;
            }
            else
            {
                result.WarningMessage = "Perfect Sync!";
                result.MessageType = MessageType.Info;
                result.IsCompatible = true;
            }
        }

        private bool IsPowerOfTwo( int x ) => ( x != 0 ) && ( ( x & ( x - 1 ) ) == 0 );

        private void DisplayValidationSummary()
        {
            if ( _validationResults.Count == 0 ) return;
            int compatible = _validationResults.Count( r => r.IsCompatible );
            int total = _validationResults.Count;

            EditorGUILayout.Space( 5 );
            if ( compatible == total )
                EditorGUILayout.HelpBox( $"All {total} images valid!", MessageType.Info );
            else
                EditorGUILayout.HelpBox( $"{compatible}/{total} valid. Fix Physical Sizes below.", MessageType.Warning );
        }

        private void DisplayDetailedValidation()
        {
            EditorGUILayout.LabelField( "Detailed Results", EditorStyles.boldLabel );
            _scrollPosition = EditorGUILayout.BeginScrollView( _scrollPosition, GUILayout.MaxHeight( 400 ) );

            foreach ( var result in _validationResults )
            {
                EditorGUILayout.BeginVertical( EditorStyles.helpBox );

                EditorGUILayout.BeginHorizontal();
                var c = GUI.color;
                GUI.color = result.IsCompatible ? Color.green : Color.red;
                GUILayout.Label( result.IsCompatible ? "[Incompatible]" : "[Compatible]", EditorStyles.boldLabel, GUILayout.Width( 20 ) );
                GUI.color = c;
                EditorGUILayout.LabelField( result.Name, EditorStyles.boldLabel );
                EditorGUILayout.EndHorizontal();

                DrawRow( "Texture:", $"{result.TextureSize.x}x{result.TextureSize.y}", result.TextureAspect );
                DrawRow( "Physical:", $"{result.PhysicalSize.x:F2}m x {result.PhysicalSize.y:F2}m", result.PhysicalAspect );

                if ( !string.IsNullOrEmpty( result.WarningMessage ) )
                {
                    EditorGUILayout.Space( 3 );
                    EditorGUILayout.HelpBox( result.WarningMessage, result.MessageType );
                }

                if ( !result.IsCompatible )
                {
                    EditorGUILayout.Space( 3 );
                    if ( GUILayout.Button( "Fix Physical Size", EditorStyles.miniButton ) )
                        AdjustPhysicalSize( result );
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space( 5 );
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRow( string label, string data, float aspect )
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField( label, GUILayout.Width( 70 ) );
            EditorGUILayout.LabelField( data, GUILayout.Width( 120 ) );
            EditorGUILayout.LabelField( $"Asp: {aspect:F2}", GUILayout.Width( 80 ) );
            EditorGUILayout.EndHorizontal();
        }

        private void AdjustPhysicalSize( ValidationResult result )
        {
            float targetAspect = result.TextureAspect;
            float newHeight = result.PhysicalSize.x / targetAspect;

            EditorUtility.DisplayDialog( "Fix Recommendation",
                $"Set '{result.Name}' Height to {newHeight:F3}m in the Reference Image Library.", "OK" );
        }
    }
}