using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Felina.ARColoringBook.DI
{
    [DefaultExecutionOrder( -1000 )]
    public class MonoContainer : MonoBehaviour
    {
        [Serializable]
        class DependencyEntry
        {
            public string TypeName;
            public string FullQualifiedName;
            public Object Instance;

            public DependencyEntry( Type type, Object instance )
            {
                TypeName = type.Name;
                FullQualifiedName = type.AssemblyQualifiedName;
                Instance = instance;
            }
        }

        [SerializeField] private List<DependencyEntry> _editorList = new();
        [SerializeField] private List<MonoBehaviour> _dependants = new();

        private readonly Dictionary<Type, object> _registry = new();

        private static readonly Dictionary<Type, (FieldInfo[], PropertyInfo[])> _reflectionCache = new();


#if UNITY_EDITOR
        private void OnValidate()
        {
            _editorList.Clear();
            foreach ( var dependant in _dependants )
            {
                if ( dependant != null )
                    Register( dependant );
            }
        }

        private void Register( object target )
        {
            var type = target.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach ( var field in type.GetFields( flags ) )
            {
                if ( Attribute.IsDefined( field, typeof( InjectAttribute ) ) )
                    AddToEditorList( field.FieldType );
            }

            foreach ( var prop in type.GetProperties( flags ) )
            {
                if ( Attribute.IsDefined( prop, typeof( InjectAttribute ) ) && prop.CanWrite )
                    AddToEditorList( prop.PropertyType );
            }
        }

        private void AddToEditorList( Type type )
        {
            if ( _editorList.Exists( x => x.FullQualifiedName == type.AssemblyQualifiedName ) )
                return;

            var instance = FindAnyObjectByType( type );
            _editorList.Add( new DependencyEntry( type, instance ) );

            UnityEditor.EditorUtility.SetDirty( this );
        }
#endif


        private void Awake()
        {
            foreach ( var entry in _editorList )
            {
                var type = Type.GetType( entry.FullQualifiedName );
                if ( type != null && entry.Instance != null )
                    _registry[ type ] = entry.Instance;
            }

            foreach ( var dependant in _dependants )
            {
                if ( dependant != null ) Resolve( dependant );
            }
        }

        public void Resolve( object target )
        {
            var type = target.GetType();

            if ( !_reflectionCache.TryGetValue( type, out var members ) )
            {
                members = GetInjectableMembers( type );
                _reflectionCache[ type ] = members;
            }

            foreach ( var field in members.Item1 )
            {
                field.SetValue( target, GetDependency( field.FieldType ) );
            }

            foreach ( var prop in members.Item2 )
            {
                prop.SetValue( target, GetDependency( prop.PropertyType ) );
            }
        }

        private object GetDependency( Type type )
        {
            if ( _registry.TryGetValue( type, out var instance ) )
                return instance;

            var found = FindAnyObjectByType( type );
            if ( found != null ) _registry[ type ] = found;
            return found;
        }

        private (FieldInfo[], PropertyInfo[]) GetInjectableMembers( Type type )
        {
            var fields = new List<FieldInfo>();
            var props = new List<PropertyInfo>();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach ( var f in type.GetFields( flags ) )
                if ( f.IsDefined( typeof( InjectAttribute ), true ) ) fields.Add( f );

            foreach ( var p in type.GetProperties( flags ) )
                if ( p.IsDefined( typeof( InjectAttribute ), true ) && p.CanWrite ) props.Add( p );

            return (fields.ToArray(), props.ToArray());
        }
    }
}
