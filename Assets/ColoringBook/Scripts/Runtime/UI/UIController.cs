using Felina.ARColoringBook.Events;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    private Canvas _canvas;

    [Header( "Visuals" )]
    [SerializeField] private Image _reticleImage;
    [SerializeField] private TextMeshProUGUI _hintText;
    [SerializeField] private Button _captureButton;
    [SerializeField] private Image _qualityBar;

    [Header( "Colors" )]
    [SerializeField] private Color _colorUnstable = Color.red;
    [SerializeField] private Color _colorBadAngle = Color.yellow;
    [SerializeField] private Color _colorReady = Color.green;

    [Header( "Thresholds" )]
    [SerializeField] private float _readyThreshold = 0.95f;

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
        if( _hintText) _hintText.text = evt.HintMessage;

        if ( _qualityBar ) _qualityBar.fillAmount = evt.QualityScore;

        if ( !evt.IsStable )
        {
            _reticleImage.color = _colorUnstable;
        }
        else if ( evt.QualityScore < _readyThreshold )
        {
            _reticleImage.color = _colorBadAngle;
        }
        else
        {
            _reticleImage.color = _colorReady;
        }
    }

    private void OnToggleUI( ToggleUIEvent args )
    {
        _canvas.enabled = args.State;
    }
}