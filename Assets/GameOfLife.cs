using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

public class GameOfLife : MonoBehaviour
{

    public KMBombInfo Info;
    public KMBombModule Module;
    public KMAudio Audio;

    public KMSelectable[] Btn;
    public KMSelectable Submit;
    public KMSelectable Reset;
    public MeshRenderer[] BtnColor;
    public TextMesh DisplayText;
    public Color32[] Colors;

    private CellColor[] BtnColor1init = new CellColor[48];
    private CellColor[] BtnColor2init = new CellColor[48];
    private CellColor[] BtnColor1 = new CellColor[48];
    private CellColor[] BtnColor2 = new CellColor[48];
    private int[] nCount = new int[48];
    private int Gen;
    private Color32[] ColorsSubmitted = new Color32[48];
    private Color32[] BtnColorStore = new Color32[48];
    private bool[] Rules = new bool[9];

    private int BlackAmount = 32; // amount of black squares generated, at average, in initial setup
    private int WhiteAmount = 12; // amount of white squares generated, at average, in initial setup
    private float TimeFlash = 0.5f; // time between flashes
    private float TimeSuspend = 0.8f; // time between generation when submitting
    private float TimeSneak = 0.4f; // time the correct solution is displayed at a strike
    private float TimeTiny = 0.01f; // time to allow computations in correct order. set to as low as possible
    private int GenRange = 3; // maximum number of generations

    private int iiLast;
    private int iiBatteries;
    private int iiLit;
    private int iiUnlit;
    private int iiPortTypes;
    private int iiStrikes;
    private int iiSolved;
    private float iiTimeRemaining;
    private float iiTimeOriginal;
    private bool Bob;

    private bool isActive;
    private bool isSolved;
    private bool isSubmitting;

    private static int moduleIdCounter = 1;
    private int moduleId;
    private string[] debugStore = new string[48];


    /////////////////////////////////////////////////// Initial Setup ///////////////////////////////////////////////////////

    // Loading screen
    void Start()
    {

        moduleId = moduleIdCounter++;
        Module.OnActivate += Activate;
    }

    // Lights off
    void Awake()
    {

        //run initial setup
        InitSetup();

        //assign button presses
        Reset.OnInteract += delegate()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Reset.transform);
            Reset.AddInteractionPunch();
            if (isActive && !isSolved && !isSubmitting)
            {
                Debug.Log("[Game of Life Cruel #" + moduleId + "] Module has been reset");
                StartCoroutine(updateReset());
            }

            return false;
        };

        Submit.OnInteract += delegate()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Submit.transform);
            Submit.AddInteractionPunch();
            if (isActive && !isSolved && !isSubmitting)
            {
                StartCoroutine(handleSubmit());
            }

            return false;
        };

        for (int i = 0; i < 48; i++)
        {
            int j = i;
            Btn[i].OnInteract += delegate()
            {
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Btn[j].transform);
                if (isActive && !isSolved && !isSubmitting)
                {
                    handleSquare(j);
                }

                return false;
            };
        }
    }

    // Lights on
    void Activate()
    {

        updateBool();

        StartCoroutine(updateTick());

        StartCoroutine(updateDebug());

        isActive = true;
    }

    // Initial setup
    void InitSetup()
    {

        Gen = Random.Range(2, GenRange + 1);
        DisplayText.text = Gen.ToString();

        iiTimeOriginal = Info.GetTime();
        Bob = true;

        for (int i = 0; i < 48; i++)
        {
            // radomizing starting squares
            int x = Random.Range(0, 48);
            if (x < BlackAmount)
            {
                // black, black
                BtnColor1init[i] = CellColor.Black;
                BtnColor2init[i] = CellColor.Black;
                BtnColor1[i] = CellColor.Black;
                BtnColor2[i] = CellColor.Black;
            }
            else
            {
                if (x < BlackAmount + WhiteAmount)
                {
                    // white, white
                    BtnColor1init[i] = CellColor.White;
                    BtnColor2init[i] = CellColor.White;
                    BtnColor1[i] = CellColor.White;
                    BtnColor2[i] = CellColor.White;
                }
                else
                {
                    // others randomized
                    BtnColor1init[i] = (CellColor)Random.Range(0, 9);
                    if (BtnColor1init[i] == CellColor.White)
                        BtnColor1init[i] = CellColor.Black;
                    BtnColor2init[i] = (CellColor)Random.Range(0, 9);
                    if (BtnColor2init[i] == CellColor.White)
                        BtnColor2init[i] = CellColor.Black;
                    BtnColor1[i] = BtnColor1init[i];
                    BtnColor2[i] = BtnColor2init[i];
                }
            }
        }
    }


    /////////////////////////////////////////////////// Updates ///////////////////////////////////////////////////////

    // update the booleans for rules
    void updateBool()
    {

        iiLast = Info.GetSerialNumberNumbers().Last();
        iiBatteries = Info.GetBatteryCount();
        iiLit = Info.GetOnIndicators().Count();
        iiUnlit = Info.GetOffIndicators().Count();
        iiPortTypes = Info.GetPorts().Distinct().Count();
        iiStrikes = Info.GetStrikes();
        iiSolved = Info.GetSolvedModuleNames().Count();
        iiTimeRemaining = Info.GetTime();

        if (iiStrikes > 0 && iiBatteries != 0)
        {
            //red		needs update
            Rules[2] = true;
        }
        else
        {
            Rules[2] = false;
        }

        if (iiTimeRemaining < iiTimeOriginal / 2 &&
            !Info.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.CAR))
        {
            //orange	needs update
            Rules[3] = true;
        }
        else
        {
            Rules[3] = false;
        }

        if (iiLit > iiUnlit && !Info.IsPortPresent(KMBombInfoExtensions.KnownPortType.RJ45))
        {
            //yellow
            Rules[4] = true;
        }
        else
        {
            Rules[4] = false;
        }

        if (iiSolved % 2 == 0 && !Info.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.CLR))
        {
            //green		needs update
            Rules[5] = true;
        }
        else
        {
            Rules[5] = false;
        }

        if (Info.GetSerialNumberLetters().Any("seaky".Contains) &&
            !Info.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.SND))
        {
            //blue
            Rules[6] = true;
        }
        else
        {
            Rules[6] = false;
        }

        if (iiLit < iiUnlit && iiBatteries < 4)
        {
            //purple
            Rules[7] = true;
        }
        else
        {
            Rules[7] = false;
        }

        if (iiPortTypes > 2 && iiLit + iiUnlit > 0)
        {
            //brown
            Rules[8] = true;
        }
        else
        {
            Rules[8] = false;
        }
    }

    // automatic update of squares
    private IEnumerator updateTick()
    {

        if (!isActive || isSubmitting)
        {
            // check if module is inactive or submitting. if yes, then wait.
            yield return new WaitForSeconds(TimeFlash);
            StartCoroutine(updateTick());
        }
        else
        {
            StartCoroutine(updateSquares());
            yield return new WaitForSeconds(TimeFlash);
            StartCoroutine(updateTick());
        }
    }

    // update the squares to correct colors
    private IEnumerator updateSquares()
    {
        for (var i = 0; i < 48; i++)
        {
            var j = i;
            if (IsColor(i, CellColor.Black))
            {
                BtnColor[j].material.color = Colors[(int)BtnColor1[j]];
            }
            else
            {
                if (IsColor(i, CellColor.White))
                {
                    BtnColor[j].material.color = Colors[(int)BtnColor1[j]];
                }
                else
                {
                    // all other cases
                    BtnColor[j].material.color = BtnColor[i].material.color == Colors[(int)BtnColor1[i]] 
                        ? Colors[(int)BtnColor2[j]] 
                        : Colors[(int)BtnColor1[j]];
                }
            }
        }

        yield return false;
    }

    // perform a reset to initial state
    private IEnumerator updateReset()
    {
        for (var r = 0; r < 48; r++)
        {
            BtnColor1[r] = BtnColor1init[r];
            BtnColor2[r] = BtnColor2init[r];
        }

        StartCoroutine(updateSquares());
        Bob = true;
        yield return new WaitForSeconds(TimeTiny);
        StartCoroutine(updateDebug());
        yield return false;
    }

    // display current state in debug log
    private IEnumerator updateDebug()
    {

        yield return new WaitForSeconds(TimeTiny);

        for (var d = 0; d < 48; d++)
        {
            if (IsColor(d, CellColor.Black))
            {
                debugStore[d] = "0";
            }
            else
            {
                if (IsColor(d, CellColor.White))
                {
                    debugStore[d] = "1";
                }
                else
                {
                    debugStore[d] = "X";
                }
            }
        }

        Debug.Log("[Game of Life Cruel #" + moduleId + "] (0 is black, 1 is white and X is colored): \n" +
                  debugStore[0] + " " + debugStore[1] + " " + debugStore[2] + " " + debugStore[3] + " " +
                  debugStore[4] + " " + debugStore[5] + "\n" +
                  debugStore[6] + " " + debugStore[7] + " " + debugStore[8] + " " + debugStore[9] + " " +
                  debugStore[10] + " " + debugStore[11] + "\n" +
                  debugStore[12] + " " + debugStore[13] + " " + debugStore[14] + " " + debugStore[15] + " " +
                  debugStore[16] + " " + debugStore[17] + "\n" +
                  debugStore[18] + " " + debugStore[19] + " " + debugStore[20] + " " + debugStore[21] + " " +
                  debugStore[22] + " " + debugStore[23] + "\n" +
                  debugStore[24] + " " + debugStore[25] + " " + debugStore[26] + " " + debugStore[27] + " " +
                  debugStore[28] + " " + debugStore[29] + "\n" +
                  debugStore[30] + " " + debugStore[31] + " " + debugStore[32] + " " + debugStore[33] + " " +
                  debugStore[34] + " " + debugStore[35] + "\n" +
                  debugStore[36] + " " + debugStore[37] + " " + debugStore[38] + " " + debugStore[39] + " " +
                  debugStore[40] + " " + debugStore[41] + "\n" +
                  debugStore[42] + " " + debugStore[43] + " " + debugStore[44] + " " + debugStore[45] + " " +
                  debugStore[46] + " " + debugStore[47]);
    }


    /////////////////////////////////////////////////// Button presses ///////////////////////////////////////////////////////

    // square is pressed
    void handleSquare(int num)
    {

        Bob = false;
        if (BtnColor[num].material.color == Colors[0])
        {
            BtnColor[num].material.color = Colors[1];
            BtnColor1[num] = CellColor.White;
            BtnColor2[num] = CellColor.White;
        }
        else
        {
            BtnColor[num].material.color = Colors[0];
            BtnColor1[num] = 0;
            BtnColor2[num] = 0;
        }
    }

	// submit is pressed
	public IEnumerator handleSubmit () {

		isSubmitting = true;
		Debug.Log ("[Game of Life Cruel #" + moduleId + "] Submit pressed. Submitted states are:");
		StartCoroutine (updateDebug ());
		yield return new WaitForSeconds (TimeTiny);

		// bob helps out? 
		if (Info.GetBatteryCount () == 6 && Info.GetBatteryHolderCount () == 3 && Bob && Info.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.BOB) && !Info.IsIndicatorOn(KMBombInfoExtensions.KnownIndicatorLabel.BOB)) {
			Module.HandlePass ();
			Debug.Log ("[Game of Life Cruel #" + moduleId + "] Bob has assisted you. Time to party!");
			for (var i = 0; i < 48; i++) {
				BtnColor1[i] = (CellColor)Random.Range (2, 9);
				BtnColor2[i] = (CellColor)Random.Range (2, 9);
			}
			isSolved = true;
			isSubmitting = false;
		} else {

			// store the submitted color values
			for (var i = 0; i < 48; i++)
			{
			    ColorsSubmitted[i] = BtnColor[i].material.color;
			}

			// run a reset
			Debug.Log ("[Game of Life Cruel #" + moduleId + "] Original states were:");
			StartCoroutine (updateReset ());
			yield return new WaitForSeconds (TimeTiny * 20);

			// transform colored squares into black or white, according to rules (update rules first)
			updateBool ();
			yield return new WaitForSeconds (TimeTiny);

		    for (var i = 0; i < 48; i++)
		    {
		        var j = i;
		        if (IsColor(i, CellColor.Black) || IsColor(i, CellColor.White))
		            continue;

		        for (var ruleIndex = 2; ruleIndex <= 8; ruleIndex++)
		        for (var colorIndex = 2; colorIndex <= 7; colorIndex++)
		        {
		            var currentRuleColor = (CellColor) ruleIndex;
		            var currentColor = (CellColor) colorIndex;

		            //Black + Color Flashing
		            if (IsColor(i, CellColor.Black, currentRuleColor))
		            {
		                if (Rules[ruleIndex] == false)
		                {
		                    BtnColor1[j] = CellColor.White;
		                    BtnColor2[j] = CellColor.White;
		                    LogState(CellColor.Black, currentRuleColor, CellColor.White);
		                    break;
		                }

		                BtnColor1[j] = CellColor.Black;
		                BtnColor2[j] = CellColor.Black;
		                LogState(CellColor.Black, currentRuleColor, CellColor.Black);
		                break;
		            }

		            //Color + Non-Brown Color Flashing
		            if (IsColor(i, CellColor.Brown) || IsColor(i, currentRuleColor, currentColor) && currentRuleColor != CellColor.Brown)
		            {
		                if (Rules[ruleIndex])
		                {
		                    BtnColor1[j] = CellColor.White;
		                    BtnColor2[j] = CellColor.White;
		                    LogState(CellColor.Black, currentRuleColor, CellColor.White);
		                    break;
		                }

		                BtnColor1[j] = CellColor.Black;
		                BtnColor2[j] = CellColor.Black;
		                LogState(CellColor.Black, currentRuleColor, CellColor.Black);
		                break;
		            }

		            //Brown
		            if (!IsColor(i, CellColor.Brown, currentRuleColor))
		                continue;

		            if (iiLast % 2 == 0)
		            {
		                if (Rules[8])
		                {
		                    BtnColor1[j] = CellColor.White;
		                    BtnColor2[j] = CellColor.White;
		                    Debug.Log("[Game of Life Cruel #" + moduleId + "] Flashing red & brown (brown rule) = White");
		                }
		                else
		                {
		                    BtnColor1[j] = CellColor.Black;
		                    BtnColor2[j] = CellColor.Black;
		                    Debug.Log("[Game of Life Cruel #" + moduleId + "] Flashing red & brown (brown rule) = Black");
		                }
		            }
		            else
		            {
		                if (Rules[2])
		                {
		                    BtnColor1[j] = CellColor.White;
		                    BtnColor2[j] = CellColor.White;
		                    Debug.Log("[Game of Life Cruel #" + moduleId + "] Flashing red & brown (red rule) = White");
		                }
		                else
		                {
		                    BtnColor1[j] = CellColor.Black;
		                    BtnColor2[j] = CellColor.Black;
		                    Debug.Log("[Game of Life Cruel #" + moduleId + "] Flashing red & brown (red rule) = Black");
		                }
		            }
		        }
		    }

		    // update squares to show state of colors fixed, then wait for sneak
			StartCoroutine (updateSquares ());
			Debug.Log ("[Game of Life Cruel #" + moduleId + "] Colored square states:");
			StartCoroutine (updateDebug ());
			yield return new WaitForSeconds (TimeSneak);

			// process the generations
			for (int g = 0; g < Gen; g++) {

				// store square color value
				for (int s = 0; s < 48; s++) {
					BtnColorStore [s] = BtnColor [s].material.color;
				}

				// process neighbours for each square
				for (int k = 0; k < 48; k++) {
					int l = k;
					nCount [l] = 0;
					// top left
					if (k - 7 < 0 || k %6 == 0) {
					} else {
						if (BtnColorStore [k - 7].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// top
					if (k - 6 < 0) {
					} else {
						if (BtnColorStore [k - 6].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// top right
					if (k - 5 < 0 || k %6 == 5) {
					} else {
						if (BtnColorStore [k - 5].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// left
					if (k - 1 < 0 || k %6 == 0) {
					} else {
						if (BtnColorStore [k - 1].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// right
					if (k + 1 > 47 || k %6 == 5) {
					} else {
						if (BtnColorStore [k + 1].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// bottom left
					if (k + 5 > 47 || k %6 == 0) {
					} else {
						if (BtnColorStore [k + 5].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// bottom
					if (k + 6 > 47) {
					} else {
						if (BtnColorStore [k + 6].Equals (Colors [1])) {
							nCount [l]++;
						}
					}
					// bottom right
					if (k + 7 > 47 || k %6 == 5) {
					} else {
						if (BtnColorStore [k + 7].Equals (Colors [1])) {
							nCount [l]++;
						}
					}

					// read nCount and decide life state
					if (BtnColor [k].material.color == Colors [1]) {	//if square is white
						if (nCount [k] < 2 || nCount [k] > 3) {
							BtnColor [l].material.color = Colors [0]; 
							BtnColor1 [l] = CellColor.Black;
							BtnColor2 [l] = CellColor.Black;
						}
					} else {											//if square is black
						if (nCount [k] == 3) {
							BtnColor [l].material.color = Colors [1];
							BtnColor1 [l] = CellColor.White;
							BtnColor2 [l] = CellColor.White;
						}
					}
				}

				// update squares, wait, then next generation
				StartCoroutine (updateSquares ());
				StartCoroutine (updateDebug ());

				if (g < Gen - 1) {
					yield return new WaitForSeconds (TimeSuspend);
				} else {
					yield return new WaitForSeconds (TimeTiny);
				}
			}

			// test last generation vs ColorsSubmitted
			for (int i = 0; i < 48; i++) {
				if (isSubmitting) {
					//is any square wrongly submitted, then strike
					if (BtnColor [i].material.color != ColorsSubmitted [i]) {
						Debug.Log ("[Game of Life Cruel #" + moduleId + "] First error found at square number " + (i + 1) + " in reading order. Strike");
						Module.HandleStrike ();
						yield return new WaitForSeconds (TimeSneak);
						isSubmitting = false;
						StartCoroutine (updateReset ());
					}
				}
			}
			//solve!
			if (isSubmitting) {
				Debug.Log ("[Game of Life Cruel #" + moduleId + "] No errors found! Module passed");
				Module.HandlePass ();
				isSolved = true;
			}

			yield return false;
		}
    }

    private void LogState(CellColor color1, CellColor color2, CellColor resultColor)
    {
        if(color1 == color2)
            Debug.Log("[Game of Life Cruel #" + moduleId + "] Steady "+color1+" = " + resultColor);

        Debug.Log("[Game of Life Cruel #" + moduleId + "] Flashing " + color1 + " and " + color2 + " = " + resultColor);
    }

    private bool IsColor(int i, CellColor color1, CellColor? color2 = null)
    {
        if (color2 == null)
            return BtnColor1[i] == color1 && BtnColor2[i] == color1;

        return BtnColor1[i] == color1 && BtnColor2[i] == color2 || BtnColor1[i] == color2 && BtnColor2[i] == color1;
    }

    //private string TwitchHelpMessage = "Set the cells with !{0} a1 a2 b2 c3 f6... Submit the current state with !{0} submit. Reset to initial state with !{0} reset";
    KMSelectable[] ProcessTwitchCommand(string inputCommand)
    {
        List<KMSelectable> buttons = new List<KMSelectable>();
        string[] split = inputCommand.ToLowerInvariant().Split(' ');
        if (split.Length == 1 && split[0] == "reset")
        {
            buttons.Add(Reset);
        }
        else if (split.Length == 1 && split[0] == "submit")
        {
            buttons.Add(Submit);
        }
        else
        {
            const string letters = "abcdef";
            const string numbers = "12345678";
            foreach (string item in split)
            {
                int x = letters.IndexOf(item.Substring(0, 1), StringComparison.Ordinal);
                int y = numbers.IndexOf(item.Substring(1, 1), StringComparison.Ordinal);
                if (item.Length != 2 || x < 0 || y < 0)
                    return null;
                buttons.Add(Btn[y * 6 + x]);
            }
        }
        return buttons.Count > 0 ? buttons.ToArray() : null;
    }
}