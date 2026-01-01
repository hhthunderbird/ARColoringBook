using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Felina.ARColoringBook.Events
{
    public class UIController : MonoBehaviour
    {
        private Canvas _canvas;

        [Header( "Visuals" )]
        [SerializeField] private Image _targetImage;
        [SerializeField] private Image _reticleImage;
        [SerializeField] private TextMeshProUGUI _hintText;
        [SerializeField] private Button _captureButton;
        [SerializeField] private Image _qualityBar;

        [Header( "Colors" )]
        [SerializeField] private Color _colorUnstable = Color.red;
        [SerializeField] private Color _colorBadAngle = Color.yellow;
        [SerializeField] private Color _colorReady = Color.green;

        public event Action OnCapture;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _canvas.enabled = false;
            EventManager.Subscribe<ToggleUIEvent>( OnToggleUI );
            EventManager.Subscribe<ScanFeedbackEvent>( OnScanFeedbackEvent );
        }

        private void Start()
        {
            _captureButton.onClick.AddListener( OnCaptureButton );
        }

        private void OnCaptureButton() => OnCapture?.Invoke();

        private void OnScanFeedbackEvent( ScanFeedbackEvent evt )
        {
            _hintText.text = evt.HintMessage;

            // Optional: Update a smooth slider
            if ( _qualityBar ) _qualityBar.fillAmount = evt.QualityScore;

            if ( !evt.IsStable )
            {
                // CASE 1: Unstable (Moving too fast)
                _reticleImage.color = _colorUnstable;
                //_scanButton.interactable = false;
            }
            else if ( evt.QualityScore < 0.95f )
            {
                // CASE 2: Stable, but bad angle/distance
                _reticleImage.color = _colorBadAngle;
                //_scanButton.interactable = false; // Or true, if you want to allow "bad" scans
            }
            else
            {
                // CASE 3: Perfect
                _reticleImage.color = _colorReady;
                //_scanButton.interactable = true;
            }
        }

        private void OnToggleUI( ToggleUIEvent args )
        {
            if ( args.State )
            {
                var tx = args.Texture;
                _targetImage.sprite = Sprite.Create( tx, new Rect( Vector2.zero, new Vector2( tx.width, tx.height ) ), Vector2.zero );
                _targetImage.preserveAspect = true;
            }

            _canvas.enabled = args.State;
        }
    }
}