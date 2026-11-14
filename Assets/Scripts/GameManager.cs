using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum BetColor { Red, Black, Green }

/// Singleton that owns chips, debt, spins and the deadline loop.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public MusicManager music;
    public RouletteWheel wheel;
    public PostFXController postFX;
    public ParticleSystem winVFX;
    public AudioSource sfx;
    public AudioClip spinClip, winClip, loseClip, paidClip;

    [Header("UI")]
    public TMP_Text atmLabel;
    public TMP_Text debtLabel;
    public TMP_Text betColorText;
    public SpinPips spinPips;
    public GameObject[] colorRings;   // Red, Black, Green ring order
    public TMP_Text chipsText;
    public TMP_Text depositedText;
    public TMP_Text debtText;
    public TMP_Text spinsText;
    public TMP_Text betText;
    public TMP_Text resultText;
    public TMP_Text rulesText;
    public float rulesShowTime = 4f;   // seconds the house rule stays on screen
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public TMP_Text gameOverBestText;
    public TMP_Text depositButtonText;
    public GameObject resultBox;      // LED panel, hidden while the wheel spins
    public float resultShowTime = 3f;  // seconds a result stays on the LED before fading out

    [Header("Balance")]
    public int startChips = 100;
    public int baseDebt = 150;
    public float debtGrowth = 1.5f;   // debt x1.5 each deadline
    public int baseSpins = 6;
    public float baseInterest = 0.05f;    // ATM interest per spin at deadline 1 (5%)
    public float interestGrowth = 0.03f;  // +3% per deadline: 5, 8, 11, 14, 17, 20%
    public float maxInterest = 0.20f;     // never above 20% per spin
    public int bonusPerSpareSpin = 10;    // flat reward per unused spin when the debt is paid early (one minimum bet back)
    public int baseDepositStep = 20;  // chips per Deposit click at deadline 1, grows like the debt, always a multiple of 10

    int chips, deposited, debt, spinsLeft, maxSpins, deadline = 1, bet = 10;
    const int startBet = 10;
    float interest;
    BetColor betColor = BetColor.Red;
    bool colorChosen;   // no color is picked until the player clicks a chip
    bool over;
    float rulesHideAt;   // when the house rule text disappears
    float resultHideAt;  // when the LED message fades out
    CanvasGroup resultFade;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        Time.timeScale = 1f;
        chips = startChips;
        gameOverPanel.SetActive(false);
        ShowResult("PLACE YOUR BET");
        StartDeadline();
    }

    // ---------- Difficulty scaling ----------
    void StartDeadline()
    {
        debt = Mathf.RoundToInt(baseDebt * Mathf.Pow(debtGrowth, deadline - 1));  // rising debt
        maxSpins = baseSpins;                                                     // same number of spins every deadline
        spinsLeft = maxSpins;
        interest = Mathf.Min(maxInterest, baseInterest + interestGrowth * (deadline - 1));   // interest grows with progress
        wheel.speedMultiplier = 1f + 0.15f * (deadline - 1);                     // faster, more chaotic wheel
        wheel.SetCracked(Mathf.Max(0, deadline - 2));                            // house rule from deadline 3
        bet = Mathf.Clamp(bet, 1, Mathf.Max(1, chips));
        postFX.SetPressure(0f);
        UpdateUI();
        if (music && deadline >= 3) music.PlaySinisterOnce();     // sinister sting for each cursed deadline
        rulesHideAt = deadline >= 3 ? Time.time + rulesShowTime : 0f;
    }

    // ---------- Buttons ----------
    public void SetColor(int c) { betColor = (BetColor)c; colorChosen = true; UpdateUI(); }
    public void ChangeBet(int delta) { bet = Mathf.Clamp(bet + delta, 1, Mathf.Max(1, chips)); UpdateUI(); }
    public void AllIn() { bet = Mathf.Max(1, chips); UpdateUI(); }

    // Deposit amount per click: rises each deadline, rounded up to the next 10 (20, 30, 50, 70, 110...)
    int DepositStep => Mathf.CeilToInt(baseDepositStep * Mathf.Pow(debtGrowth, deadline - 1) / 10f) * 10;
    // What one click actually deposits: the step, or just what's still missing from the debt (e.g. 140/150 -> 10, 147/150 -> 3)
    int DepositAmount => Mathf.Min(DepositStep, Mathf.Max(0, debt - deposited));

    public void Deposit()
    {
        if (over || wheel.Spinning) return;
        if (chips < DepositAmount)   // not enough chips -> error feedback, keep spinning
        {
            Play(loseClip);
            postFX.LoseFlash();
            return;
        }
        int amount = DepositAmount;                   // fixed step, capped at what the debt still needs
        chips -= amount;
        deposited += amount;
        bet = Mathf.Clamp(bet, 1, Mathf.Max(1, chips));
        CheckDeadline();
        UpdateUI();
    }

    void Update()
    {
        // House rule shows for a few seconds at the start of a cursed deadline (pausing freezes the timer)
        if (rulesText) rulesText.gameObject.SetActive(Time.time < rulesHideAt);

        // LED: fully visible, then a half-second fade at the end (pausing freezes it)
        if (resultBox)
        {
            float left = resultHideAt - Time.time;
            resultBox.SetActive(left > 0f);
            if (resultFade) resultFade.alpha = Mathf.Clamp01(left / 0.5f);
        }
    }

    void ShowResult(string msg)
    {
        resultText.text = msg;
        resultHideAt = Time.time + resultShowTime;
        if (resultBox && !resultFade)
        {
            resultFade = resultBox.GetComponent<CanvasGroup>();
            if (!resultFade) resultFade = resultBox.AddComponent<CanvasGroup>();
        }
    }

    public void Spin()
    {
        if (over || wheel.Spinning || spinsLeft <= 0 || bet <= 0 || bet > chips) return;
        if (!colorChosen)   // must pick a color first
        {
            Play(loseClip);
            postFX.LoseFlash();
            ShowResult("PICK A COLOR");
            return;
        }
        chips -= bet;
        spinsLeft--;
        rulesHideAt = 0f;   // hide the house rule as soon as the player spins
        Play(spinClip);
        resultHideAt = 0f;   // LED off while the wheel spins
        wheel.Spin(OnResult);
        UpdateUI();
    }

    // ---------- Spin result ----------
    void OnResult(int number)
    {
        BetColor landed = number == 0 ? BetColor.Green : (RouletteWheel.IsRed(number) ? BetColor.Red : BetColor.Black);
        colorChosen = false;   // every new spin starts without a color
        string label = number + " " + landed.ToString().ToUpper();

        if (wheel.IsCracked(number))
        {
            ShowResult(label + "  CURSED  NO PAY");
            Play(loseClip); postFX.LoseFlash();
        }
        else if (landed == betColor)
        {
            int payout = bet * (landed == BetColor.Green ? 36 : 2);
            chips += payout;
            ShowResult(label + "  WIN " + payout);
            Play(winClip); postFX.WinFlash();
            if (winVFX) winVFX.Play();
        }
        else
        {
            ShowResult(label + "  LOSE");
            Play(loseClip); postFX.LoseFlash();
        }

        deposited += Mathf.FloorToInt(deposited * interest);   // ATM interest
        postFX.SetPressure(1f - spinsLeft / (float)maxSpins);
        bet = Mathf.Clamp(startBet, 1, Mathf.Max(1, chips));   // back to the starting bet after every spin

        CheckDeadline();
        UpdateUI();
    }

    // Decides what happens after a spin or a deposit
    void CheckDeadline()
    {
        if (deposited >= debt) { EndDeadline(); return; }                     // debt paid (by deposit or interest)

        bool canSpin = spinsLeft > 0 && chips > 0;
        bool canStillPay = deposited + chips >= debt;                          // enough money left to deposit the rest
        if (!canSpin && !canStillPay) { EndDeadline(); return; }               // no way out -> trapdoor

        if (spinsLeft == 0) ShowResult("OUT OF SPINS  DEPOSIT TO PAY");  // spins gone, but the player can still pay
    }

    void EndDeadline()
    {
        if (deposited >= debt)
        {
            chips += deposited - debt;   // surplus comes back as chips
            int bonus = spinsLeft * bonusPerSpareSpin;   // reward for paying early, e.g. 5 spins left = 50
            chips += bonus;
            deposited = 0;
            deadline++;
            Play(paidClip);
            ShowResult(bonus > 0 ? "DEBT PAID  BONUS " + bonus : "DEBT PAID");
            if (chips <= 0) { GameOver(); return; }
            StartDeadline();
        }
        else GameOver();
    }

    void GameOver()
    {
        over = true;
        if (music) music.FadeOut();   // silence the soundtrack behind the game over screen
        int best = PlayerPrefs.GetInt("BestDeadline", 0);
        if (deadline > best) { best = deadline; PlayerPrefs.SetInt("BestDeadline", best); PlayerPrefs.Save(); }
        gameOverText.text = deadline.ToString();
        gameOverBestText.text = best.ToString();
        gameOverPanel.SetActive(true);
    }

    public void Retry() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void ToMenu() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }

    // ---------- Helpers ----------
    void Play(AudioClip c) { if (sfx && c) sfx.PlayOneShot(c); }

    void UpdateUI()
    {
        chipsText.text = chips.ToString();
        depositedText.text = deposited.ToString();
        debtText.text = debt.ToString();
        spinsText.text = spinsLeft.ToString();
        if (atmLabel) atmLabel.text = "ATM  +" + Mathf.RoundToInt(interest * 100) + "%";
        if (debtLabel) debtLabel.text = "DEADLINE " + deadline + "  DEBT";
        if (spinPips) spinPips.Show(spinsLeft, maxSpins);
        if (depositButtonText)
        {
            depositButtonText.text = "DEPOSIT " + DepositAmount;
            depositButtonText.alpha = chips >= DepositAmount ? 1f : 0.35f;   // dimmed when you can't afford a step
        }

        betText.text = bet.ToString();
        if (betColorText)
        {
            if (!colorChosen)
            {
                betColorText.text = "PICK A COLOR";
                betColorText.color = new Color(0.6f, 0.58f, 0.52f);
            }
            else
            {
                betColorText.text = "ON " + betColor.ToString().ToUpper();
                betColorText.color = betColor == BetColor.Red ? new Color(0.88f, 0.29f, 0.29f)
                                : betColor == BetColor.Green ? new Color(0.2f, 0.75f, 0.4f)
                                : new Color(0.75f, 0.75f, 0.75f);
            }
        }
        for (int i = 0; i < colorRings.Length; i++)
            if (colorRings[i]) colorRings[i].SetActive(colorChosen && i == (int)betColor);

        rulesText.text = deadline >= 3 ? "HOUSE RULE: " + (deadline - 2) + " CURSED POCKET(S)" : "";
    }
}