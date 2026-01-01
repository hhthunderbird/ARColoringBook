using UnityEngine;

namespace Felina.ARColoringBook.Events
{
    public abstract class AppEvent { }

    public class ToggleUIEvent : AppEvent
    {
        public bool State;
        public Texture2D Texture;

        public ToggleUIEvent( bool state, Texture2D texture )
        {
            State = state;
            Texture = texture;
        }
    }

    public class ScanFeedbackEvent : AppEvent
    {
        public bool IsStable;
        public float QualityScore;
        public string HintMessage;

        public void Set ( bool isStable, float quality )
        {
            IsStable = isStable;
            QualityScore = quality;

            if ( !isStable )
                HintMessage = "Hold Still";
            else if ( quality < 0.4f )
                HintMessage = "Move Closer";
            else if ( quality < 0.7f )
                HintMessage = "Adjust Angle";
            else
                HintMessage = "Ready to Scan!";
        }
    }
}
