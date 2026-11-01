using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WandController : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.01f;
    [SerializeField] private Transform wandPivot;
    [SerializeField] private Transform mouseRef;
    [SerializeField] private Text lastPatternMatchText;
    [SerializeField] private InputAction inputAction;
    [SerializeField] private string patternsPath;
    [SerializeField] private SpellListGenerator spellListGenerator;
    [SerializeField] private ParticleSystem castEffect;

    bool isCastingSpell = false;
    bool isRedirectingSpell = false;

    private Vector3 initialMouseRefPosition;
    private Coroutine resetMouseRefCoroutine;
    private PatternDetector patternDetector;
    private PatternDetector.PatternMatch lastPatternMatch;

    public System.Action<bool> OnSpellRedirected;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        initialMouseRefPosition = mouseRef.localPosition;
        inputAction.started += ctx => StartCast();
        inputAction.canceled += ctx => CancelCast();
        inputAction.Enable();

        if (!string.IsNullOrEmpty(patternsPath))
        {
            patternDetector = new PatternDetector();
            StartCoroutine(patternDetector.LoadPatterns(patternsPath, () =>
            {
                spellListGenerator.Generate(patternDetector.templates);
            }));
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!isCastingSpell)
        {
            return;
        }
        UpdateCastStates();
        MoveWand();
        AlignWand();
    }

    void StartCast()
    {
        isCastingSpell = true;
        if (castEffect != null)
            castEffect.Play();
        if (patternDetector != null)
            patternDetector.ClearRecordedPositions();
        if (resetMouseRefCoroutine != null)
        {
            StopCoroutine(resetMouseRefCoroutine);
            resetMouseRefCoroutine = null;
        }
    }

    void CancelCast()
    {
        isCastingSpell = false;
        if (castEffect != null)
            castEffect.Stop();
        if (patternDetector != null)
        {
            lastPatternMatch = patternDetector.FindPattern();
            patternDetector.ClearRecordedPositions();
            DisplayLastPatternMatch();
        }
        if (resetMouseRefCoroutine == null)
            resetMouseRefCoroutine = StartCoroutine(ResetMouseRefPosition());
    }

    void UpdateCastStates()
    {
        OnSpellRedirected?.Invoke(isRedirectingSpell);
        isRedirectingSpell = Mouse.current.rightButton.isPressed;
    }

    void MoveWand()
    {
        if (isRedirectingSpell)
            return;
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        Vector3 movement = new Vector3(
            mouseDelta.x,
            mouseDelta.y,
            0f
        );

        mouseRef.localPosition += movement * sensitivity * Time.deltaTime;

        if (patternDetector != null)
        {
            if (isCastingSpell)
                patternDetector.RecordPosition(mouseRef.localPosition);
        }
    }

    void AlignWand()
    {
        wandPivot.LookAt(mouseRef);
    }

    IEnumerator ResetMouseRefPosition()
    {
        while (Vector3.Distance(mouseRef.localPosition, initialMouseRefPosition) > float.Epsilon)
        {
            mouseRef.localPosition = Vector3.Lerp(mouseRef.localPosition, initialMouseRefPosition, Time.deltaTime);
            AlignWand();
            yield return null;
        }
        resetMouseRefCoroutine = null;
    }

    void DisplayLastPatternMatch()
    {
        if (lastPatternMatch != null)
        {
            if (lastPatternMatchText != null)
                lastPatternMatchText.text = "Last Pattern Match: " + lastPatternMatch.Name;
        }
        else
        {
            if (lastPatternMatchText != null)
                lastPatternMatchText.text = "";
        }
    }
}
