using UnityEditor;
using UnityEngine;

namespace Felina.ARColoringBook.Editor
{
    [CustomEditor( typeof( LicenseManager ) )]
    public class LicenseManagerEditor : UnityEditor.Editor
    {
        private LicenseManager _targetScript;

        private void OnEnable()
        {
            _targetScript = ( LicenseManager ) target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();
            if ( GUILayout.Button( "Create Settings Now" ) )
            {

            }
        }
    }
}