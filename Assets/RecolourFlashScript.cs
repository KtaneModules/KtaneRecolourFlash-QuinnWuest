using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using RecolourFlash;

public class RecolourFlashScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable YesButton;
    public KMSelectable NoButton;
    public TextMesh ScreenText;
    public GameObject[] ButtonObjs;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private static readonly string[] _chartWords = new string[] { "DONE", "ADD", "FIND", "EAST", "PORT", "BOOM", "LIME", "ECHO", "CALL", "LOOK", "ZERO", "XRAY", "YES", "HELP", "BEEP", "TRUE", "MIKE", "EDGE", "RED", "WORD", "WORK", "TEST", "JINX", "GOLF", "TALK", "SIX", "LIST", "MATH", "NEXT", "READ", "LIMA", "FOUR" };
    private static readonly string[] _colourNames = new string[] { "RED", "YELLOW", "GREEN", "WHITE", "BLUE", "MAGENTA" };
    private static readonly Color32[] _colours = new Color32[] { new Color32(255, 0, 0, 255), new Color32(255, 255, 0, 255), new Color32(0, 255, 0, 255), new Color32(255, 255, 255, 255), new Color32(0, 0, 255, 255), new Color32(255, 0, 255, 255) };

    private int _lastTimerSecond;
    private bool _isEvenDigit;
    private int _pressesDuringTimerTick;

    private char[] _field;
    private const int _w = 4;
    private const int _h = 4;
    private string _solution;
    private int _solutionStart;
    private int _solutionEnd;
    private int? _selectedStart;
    private int? _selectedEnd;

    private int[][] _semaphores = new int[26][]
    {
        new int[2]{4, 5 }, // A
        new int[2]{4, 6 }, // B
        new int[2]{4, 7 }, // C
        new int[2]{4, 0 }, // D
        new int[2]{4, 1 }, // E
        new int[2]{4, 2 }, // F
        new int[2]{4, 3 }, // G
        new int[2]{5, 6 }, // H
        new int[2]{5, 7 }, // I
        new int[2]{0, 2 }, // J
        new int[2]{5, 0 }, // K
        new int[2]{5, 1 }, // L
        new int[2]{5, 2 }, // M
        new int[2]{5, 3 }, // N
        new int[2]{6, 7 }, // O
        new int[2]{0, 6 }, // P
        new int[2]{1, 6 }, // Q
        new int[2]{2, 6 }, // R
        new int[2]{3, 6 }, // S
        new int[2]{0, 7 }, // T
        new int[2]{1, 7 }, // U
        new int[2]{0, 3 }, // V
        new int[2]{1, 2 }, // W
        new int[2]{1, 3 }, // X
        new int[2]{2, 7 }, // Y
        new int[2]{2, 3 }, // Z
    };

    private int[][] _flashes = new int[8][]
    {
        new int[2],
        new int[2],
        new int[2],
        new int[2],
        new int[2],
        new int[2],
        new int[2],
        new int[2]
    };

    private int _curPos;
    private int _curRow;
    private int _curCol;
    private bool _yesPressed;
    private int _curFlash;

    private Coroutine _flashSequence;
    private Coroutine[] _pressAnimations = new Coroutine[2];

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        YesButton.OnInteract += YesPress;
        NoButton.OnInteract += NoPress;
        YesButton.OnInteractEnded += YesRelease;
        NoButton.OnInteractEnded += NoRelease;

        _solution = _chartWords[Rnd.Range(0, _chartWords.Length)];
        GenerateWordSearch();
        _curRow = Rnd.Range(0, 4);
        _curCol = Rnd.Range(0, 4);
        _curPos = CalcCurPos();

        Debug.LogFormat("[Recolour Flash #{0}] The chosen word is {1}.", _moduleId, _solution);
        Debug.LogFormat("[Recolour Flash #{0}] Starting position: {1}", _moduleId, CalcCoord(_curPos));
        GenerateSemaphoreFlash();
        _flashSequence = StartCoroutine(FlashSequence());

        Debug.LogFormat("[Recolour Flash #{0}] Solution start: {1} > Solution end: {2}.", _moduleId, CalcCoord(_solutionStart), CalcCoord(_solutionEnd));
    }

    private bool YesPress()
    {
        YesButton.AddInteractionPunch(0.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        if (_pressAnimations[0] != null)
            StopCoroutine(_pressAnimations[0]);
        _pressAnimations[0] = StartCoroutine(PressAnimation(0, true));
        if (!_moduleSolved)
        {
            _yesPressed = true;
            _pressesDuringTimerTick++;
        }
        return false;
    }

    private bool NoPress()
    {
        NoButton.AddInteractionPunch(0.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        if (_pressAnimations[1] != null)
            StopCoroutine(_pressAnimations[1]);
        _pressAnimations[1] = StartCoroutine(PressAnimation(1, true));
        if (!_moduleSolved)
        {
            _yesPressed = false;
            _pressesDuringTimerTick++;
        }
        return false;
    }

    private void YesRelease()
    {
        if (_pressAnimations[0] != null)
            StopCoroutine(_pressAnimations[0]);
        _pressAnimations[0] = StartCoroutine(PressAnimation(0, false));
    }

    private void NoRelease()
    {
        if (_pressAnimations[1] != null)
            StopCoroutine(_pressAnimations[1]);
        _pressAnimations[1] = StartCoroutine(PressAnimation(1, false));
    }

    private IEnumerator PressAnimation(int btn, bool pushIn)
    {
        var duration = 0.1f;
        var elapsed = 0f;
        var curPos = ButtonObjs[btn].transform.localPosition;
        while (elapsed < duration)
        {
            ButtonObjs[btn].transform.localPosition = new Vector3(curPos.x, Easing.InOutQuad(elapsed, curPos.y, pushIn ? 0.01f : 0.0146f, duration), curPos.z);
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    private int CalcCurPos()
    {
        return (_curRow * 4) + _curCol;
    }

    private string CalcCoord(int pos)
    {
        string s = "";
        s += "ABCD"[pos % 4];
        s += "1234"[pos / 4];
        return s;
    }

    private void Update()
    {
        var curTime = (int)BombInfo.GetTime() % 2;
        if (_lastTimerSecond != curTime)
        {
            _lastTimerSecond = curTime;
            _isEvenDigit = _lastTimerSecond % 2 == 0;
            if (_pressesDuringTimerTick > 0)
            {
                if (_pressesDuringTimerTick == 1)
                {
                    if (_yesPressed)
                    {
                        if (!_isEvenDigit) // As the press was the previous one
                        {
                            _curCol = (_curCol + 1) % 4;
                            _curPos = CalcCurPos();
                            Debug.LogFormat("[Recolour Flash #{0}] Pressed YES on an even digit. Moving right to {1}.", _moduleId, CalcCoord(_curPos));
                        }
                        else
                        {
                            _curCol = (_curCol + 3) % 4;
                            _curPos = CalcCurPos();
                            Debug.LogFormat("[Recolour Flash #{0}] Pressed YES on an odd digit. Moving left to {1}.", _moduleId, CalcCoord(_curPos));
                        }

                    }
                    else
                    {
                        if (!_isEvenDigit) // As the press was the previous one
                        {
                            _curRow = (_curRow + 1) % 4;
                            _curPos = CalcCurPos();
                            Debug.LogFormat("[Recolour Flash #{0}] Pressed NO on an even digit. Moving down to {1}.", _moduleId, CalcCoord(_curPos));
                        }
                        else
                        {
                            _curRow = (_curRow + 3) % 4;
                            _curPos = CalcCurPos();
                            Debug.LogFormat("[Recolour Flash #{0}] Pressed NO on an odd digit. Moving up to {1}.", _moduleId, CalcCoord(_curPos));
                        }
                    }
                    Audio.PlaySoundAtTransform("SemaphorePress", transform);
                    GenerateSemaphoreFlash();
                }
                else
                {
                    Debug.LogFormat("[Recolour Flash #{0}] More than one press during timer tick. Submit current position.", _moduleId, CalcCoord(_curPos));
                    if (_selectedStart == null)
                    {
                        _selectedStart = _curPos;
                        Audio.PlaySoundAtTransform("WSPress", transform);
                        Debug.LogFormat("[Recolour Flash #{0}] Selected {1} as the starting cell.", _moduleId, CalcCoord(_curPos));
                    }
                    else if (_selectedStart == _curPos)
                    {
                        _selectedStart = null;
                        Audio.PlaySoundAtTransform("WSDeselect", transform);
                        Debug.LogFormat("[Recolour Flash #{0}] Deselected {1} as the starting cell.", _moduleId, CalcCoord(_curPos));
                    }
                    else
                    {
                        _selectedEnd = _curPos;
                        Debug.LogFormat("[Recolour Flash #{0}] Selected {1} as the ending cell.", _moduleId, CalcCoord(_curPos));
                        if (_selectedStart == _solutionStart && _selectedEnd == _solutionEnd)
                        {
                            Debug.LogFormat("[Recolour Flash #{0}] Successfully selected {1}. Module solved.", _moduleId, _solution);
                            StartCoroutine(SolveAnimation());
                        }
                        else
                        {
                            Debug.LogFormat("[Recolour Flash #{0}] Did not select {1}. Strike.", _moduleId, _solution);
                            StartCoroutine(StrikeAnimation());
                        }
                    }
                }
                _pressesDuringTimerTick = 0;
            }
        }
    }

    private IEnumerator SolveAnimation()
    {
        Audio.PlaySoundAtTransform("WSCorrect", transform);
        yield return new WaitForSeconds(0.5f);
        _moduleSolved = true;
        Module.HandlePass();
        StopCoroutine(_flashSequence);
        ScreenText.text = _solution;
        ScreenText.color = new Color32(255, 255, 255, 255);
    }

    private IEnumerator StrikeAnimation()
    {
        Audio.PlaySoundAtTransform("WSWrong", transform);
        yield return new WaitForSeconds(0.5f);
        Module.HandleStrike();
        _selectedStart = null;
        _selectedEnd = null;
    }

    private IEnumerator FlashSequence()
    {
        while (!_moduleSolved)
        {
            ScreenText.text = _colourNames[_flashes[_curFlash][0]];
            ScreenText.color = _colours[_flashes[_curFlash][1]];
            if (_curFlash == 0)
            {
                for (int i = 0; i < 7; i++)
                {
                    if (i % 2 == 0)
                    {
                        ScreenText.text = _colourNames[_flashes[_curFlash][0]];
                        ScreenText.color = _colours[_flashes[_curFlash][1]];
                    }
                    else
                    {
                        ScreenText.text = "";
                    }
                    yield return new WaitForSeconds(0.1f);
                }
            }
            _curFlash = (_curFlash + 1) % 8;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void GenerateSemaphoreFlash()
    {
        var curLetter = Array.IndexOf("ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray(), _field[_curPos]);
        for (int i = 0; i < _flashes.Length; i++)
        {
            tryAgain2:
            _flashes[i][0] = Rnd.Range(0, 6);
            if (_semaphores[curLetter].Contains(i))
                _flashes[i][1] = _flashes[i][0];
            else
            {
                tryAgain3:
                _flashes[i][1] = Rnd.Range(0, 6);
                if (_flashes[i][1] == _flashes[i][0])
                    goto tryAgain3;
            }
            if ((_flashes[i][0] == _flashes[(i + 7) % 8][0] && _flashes[i][1] == _flashes[(i + 7) % 8][1]) || (_flashes[i][0] == _flashes[(i + 1) % 8][0] && _flashes[i][1] == _flashes[(i + 1) % 8][1]))
                goto tryAgain2;
        }
        ScreenText.text = _colourNames[_flashes[_curFlash][0]];
        ScreenText.color = _colours[_flashes[_curFlash][1]];
    }

    private void GenerateWordSearch()
    {
        tryAgain:
        _field = new char[_w * _h];

        var coords = Enumerable.Range(0, _w * _h).ToList();
        var directions = new[] { WordDirection.Down, WordDirection.DownRight, WordDirection.Right, WordDirection.UpRight, WordDirection.Up, WordDirection.UpLeft, WordDirection.Left, WordDirection.DownLeft };

        coords.Shuffle();
        foreach (var coord in coords)
            foreach (var dir in directions.Shuffle())
                if (TryPlaceWord(_solution, coord % _w, coord / _w, dir))
                {
                    _solutionStart = coord;
                    var dx = new[] { 0, 1, 1, 1, 0, 3, 3, 3 };
                    var dy = new[] { 1, 1, 0, 3, 3, 3, 0, 1 };
                    _solutionEnd = 4 * ((coord / 4 + dy[(int)dir] * (_solution.Length - 1)) % 4) + (coord % 4 + dx[(int)dir] * (_solution.Length - 1)) % 4;
                    goto initialPlaced;
                }


        initialPlaced:;
        for (int i = 0; i < _w * _h; i++)
            if (_field[i] == '\0')
                _field[i] = (char)('A' + Rnd.Range(0, 26));

        // Make sure that the field doesn’t by chance contain one of the wrong words

        var temp = new string[] { _solution };
        var wrongWords = _chartWords.Except(temp);
        foreach (var wrong in wrongWords)
            foreach (var coord in coords)
                foreach (var dir in directions)
                    if (TryPlaceWord(wrong, coord % _w, coord / _w, dir))
                    {
                        Debug.LogFormat("<Recolour Flash #{4}> Wrong word {0} happens to come up in grid at {1},{2},{3}. Restarting.", wrong, coord % _w, coord / _w, dir, _moduleId);
                        goto tryAgain;
                    }

        Debug.LogFormat("[Recolour Flash #{0}] Field:", _moduleId);
        for (int r = 0; r < 4; r++)
            Debug.LogFormat("[Recolour Flash #{0}] {1} {2} {3} {4} ", _moduleId, _field[r * 4 + 0], _field[r * 4 + 1], _field[r * 4 + 2], _field[r * 4 + 3]);
    }

    private bool TryPlaceWord(string word, int x, int y, WordDirection dir)
    {
        switch (dir)
        {
            case WordDirection.Down:
                for (int j = 0; j < word.Length; j++)
                    if (_field[x + _w * ((y + j) % 4)] != '\0' && _field[x + _w * ((y + j) % 4)] != word[j])
                        return false;
                for (int j = 0; j < word.Length; j++)
                    _field[x + _w * ((y + j) % 4)] = word[j];
                return true;
            case WordDirection.DownRight:
                for (int j = 0; j < word.Length; j++)
                    if (_field[((x + j) % 4) + _w * ((y + j) % 4)] != '\0' && _field[((x + j) % 4) + _w * ((y + j) % 4)] != word[j])
                        return false;
                for (int j = 0; j < word.Length; j++)
                    _field[((x + j) % 4) + _w * ((y + j) % 4)] = word[j];
                return true;
            case WordDirection.Right:
                for (int j = 0; j < word.Length; j++)
                    if (_field[((x + j) % 4) + _w * y] != '\0' && _field[((x + j) % 4) + _w * y] != word[j])
                        return false;
                for (int j = 0; j < word.Length; j++)
                    _field[((x + j) % 4) + _w * y] = word[j];
                return true;
            case WordDirection.UpRight:
                for (int j = 0; j < word.Length; j++)
                    if (_field[((x + j) % 4) + _w * ((y - j + 4) % 4)] != '\0' && _field[((x + j) % 4) + _w * ((y - j + 4) % 4)] != word[j])
                        return false;
                for (int j = 0; j < word.Length; j++)
                    _field[((x + j) % 4) + _w * ((y - j + 4) % 4)] = word[j];
                return true;
            case WordDirection.Up:
                for (int j = 0; j < word.Length; j++)
                    if (_field[x + _w * ((y - j + 4) % 4)] != '\0' && _field[x + _w * ((y - j + 4) % 4)] != word[j])
                        return false;
                for (int j = 0; j < word.Length; j++)
                    _field[x + _w * ((y - j + 4) % 4)] = word[j];
                return true;
            case WordDirection.UpLeft:
                for (int j = 0; j < word.Length; j++)
                    if (_field[((x - j + 4) % 4) + _w * ((y - j + 4) % 4)] != '\0' && _field[((x - j + 4) % 4) + _w * ((y - j + 4) % 4)] != word[j])
                        return false;
                for (int j = 0; j < word.Length; j++)
                    _field[((x - j + 4) % 4) + _w * ((y - j + 4) % 4)] = word[j];
                return true;
            case WordDirection.Left:
                for (int j = 0; j < word.Length; j++)
                    if (_field[((x - j + 4) % 4) + _w * y] != '\0' && _field[((x - j + 4) % 4) + _w * y] != word[j])
                        return false;
                for (int j = 0; j < word.Length; j++)
                    _field[((x - j + 4) % 4) + _w * y] = word[j];
                return true;
            case WordDirection.DownLeft:
                for (int j = 0; j < word.Length; j++)
                    if (_field[((x - j + 4) % 4) + _w * ((y + j) % 4)] != '\0' && _field[((x - j + 4) % 4) + _w * ((y + j) % 4)] != word[j])
                        return false;
                for (int j = 0; j < word.Length; j++)
                    _field[((x - j + 4) % 4) + _w * ((y + j) % 4)] = word[j];
                return true;
        }
        return false;
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} press yes/no even/odd/twice. [Press the YES or NO button on an even digit, odd digit, or twice.]";
#pragma warning restore 0414
    private IEnumerator Press(KMSelectable btn, float delay = 0.1f)
    {
        btn.OnInteract();
        yield return new WaitForSeconds(delay);
        if (btn.OnInteractEnded != null)
            btn.OnInteractEnded();
    }
    private IEnumerator PressOnParity(int parity, KMSelectable btn, float delay = 0.1f)
    {
        yield return new WaitUntil(() => (int)BombInfo.GetTime() % 2 != parity % 2);
        int curParity = (int)BombInfo.GetTime() % 2;
        yield return Press(btn, delay);
        yield return new WaitUntil(() => (int)BombInfo.GetTime() % 2 != curParity);
    }
    private IEnumerator DoubleTap(KMSelectable btn, float delay = 0.1f)
    {
        int cd = (int)BombInfo.GetTime();
        while ((int)BombInfo.GetTime() == cd)
            yield return null;
        yield return Press(btn, delay);
        yield return new WaitForSeconds(delay);
        yield return Press(btn, delay);
    }
    private IEnumerator ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^\s*(press\s+)?((?<yes>yes)|no)\s+((?<even>even)|(?<odd>odd)|twice)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            yield break;
        yield return null;
        yield return "solve";
        yield return "strike";
        var btn = m.Groups["yes"].Success ? YesButton : NoButton;
        if (m.Groups["even"].Success)
            yield return PressOnParity(0, btn);
        else if (m.Groups["odd"].Success)
            yield return PressOnParity(1, btn);
        else
            yield return DoubleTap(btn);
    }
    private IEnumerator TwitchHandleForcedSolve()
    {
        if (_selectedStart != _solutionStart)
        {
            if (_selectedStart != null)
                yield return NavigateToAndSubmit(_selectedStart.Value);
            yield return NavigateToAndSubmit(_solutionStart);
        }
        yield return NavigateToAndSubmit(_solutionEnd);

    }
    private IEnumerator NavigateToAndSubmit(int goal)
    {
        int goalRow = goal / 4;
        int goalCol = goal % 4;
        int rowParity = ((_curRow + 3) % 4) == goalRow ? 0 : 1; //If the goal is directly below us, go down. Otherwise, it doesn't rly matter.
        int colParity = ((_curCol + 3) % 4) == goalCol ? 0 : 1; //If the goal is directly to the right of us, go right, otherwise it doesn't rly matter again.
        while (_curRow != goalRow)
            yield return PressOnParity(rowParity, NoButton);
        while (_curCol != goalCol)
            yield return PressOnParity(colParity, YesButton);
        yield return DoubleTap(NoButton);
        while (!_moduleSolved && _selectedEnd != null)
            yield return true;
    }
}
