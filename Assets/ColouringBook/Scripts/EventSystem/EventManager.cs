using System;
using System.Collections.Generic;

namespace Felina.ARColoringBook.Events
{
    public static class EventManager
    {
        private static Dictionary<Type, Delegate> _eventDictionary = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>( Action<T> listener ) where T : AppEvent
        {
            Type eventType = typeof( T );

            if ( !_eventDictionary.ContainsKey( eventType ) )
            {
                _eventDictionary[ eventType ] = null;
            }

            _eventDictionary[ eventType ] = ( Action<T> ) _eventDictionary[ eventType ] + listener;
        }

        public static void Unsubscribe<T>( Action<T> listener ) where T : AppEvent
        {
            Type eventType = typeof( T );

            if ( _eventDictionary.ContainsKey( eventType ) )
            {
                _eventDictionary[ eventType ] = ( Action<T> ) _eventDictionary[ eventType ] - listener;
            }
        }

        public static void TriggerEvent<T>( T eventInstance ) where T : AppEvent
        {
            Type eventType = typeof( T );

            if ( _eventDictionary.TryGetValue( eventType, out Delegate d ) )
            {
                Action<T> action = d as Action<T>;
                action?.Invoke( eventInstance );
            }
        }
    }
}
