using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EditableExperiment : MonoBehaviour
{
	public TextDisplayer textDisplayer;
    public TextDisplayer fullscreenTextDisplayer;
    public UnityEngine.UI.InputField inputField;
    public UnityEngine.UI.Dropdown dropDown;

    public ScriptedEventReporter scriptedEventReporter;
    public SoundRecorder soundRecorder;
    public VoiceActivityDetection voiceActivityDetection;
    public GameObject tooSoonWarning;

    public GameObject microphoneTestMessage;

    public AudioSource audioPlayback;
    public AudioSource highBeep;
    public AudioSource lowBeep;


    private string[] words;
    private Dictionary<string, int> numberingWords;

    private const string FIRST_INSTRUCTIONS_MESSAGE = 
        "\n\n\nWe will now review the basics of the study, and the experimenter will answer any questions that you have.\n\n1) Words will come onscreen one at a time.\n2) After each word leaves the screen, pause briefly, then speak the word you just saw.\n3) If you began speaking too early, a message will appear onscreen to notify you. Try to minimize the number of trials where this occurs.\n4) You will be given 10-second breaks periodically throughout the session, as well as two longer mid-session breaks.\n\n\n";

    private const string SECOND_INSTRUCTIONS_MESSAGE =
        "\n\n\n5) It is very important for you to avoid all unnecessary motion while engaged in the study. \n6) Please try to avoid blinking from the time that a word appears on the screen until you have spoken the word. \n7) If you miss a word, just say 'pass' to proceed to the next word.\n\n\nYou are now ready to begin the study! \n\nIf you have any remaining questions, please ask the experimenter now. Otherwise, press RETURN to continue.\n\n\n";

    private const string BREAK_MESSAGE =
        "\n\n\nWe will now take some time\nto readjust the electrodes.\nWhen it is time to continue,\npress SPACE and RETURN.\n\n\n";

    private const string EXPERIMENTER_MESSAGE =
        "Researcher: Please confirm that the impedance window is closed and that sync pulses are showing.";

    private const string FIRST_INITIAL_FREE_RECALL_MESSAGE =
        "\n\n\nWe would like you to recall as many words as you can remember from all previous sessions, in any order. You will have ten minutes to perform this recall task as soon as the prompt appears ('******'). \n\nAs you attempt to recall these words, other words that did not appear in previous sessions may come to mind. Please go ahead and say these words aloud even if you believe they have not been presented or if you have already said them during this recall period.\n\n\n";

    private const string SECOND_INITIAL_FREE_RECALL_MESSAGE = 
        "\n\n\nYou will receive a recall bonus of up to $5 in addition to your blink and trial performance bonuses. \n\nThe recall bonus will increase with the number of words from the previous sessions you recall. \nIt will not be affected by any words you say that were not shown in previous sessions or that you have already recalled.\n\nPress RETURN to begin.\n\n\n";

    private const string FIRST_FINAL_FREE_RECALL_MESSAGE = ""; 

    private const string SECOND_FINAL_FREE_RECALL_MESSAGE = "";

    private const string FILE_USER = "/Users/exp/";

    private const float MIN_IFR_TIME = 10.0f; 
    private const float MIN_FFR_TIME = 20.0f;
    private const float IFR_TIMEOUT = 5.0f;
    private const float FFR_TIMEOUT = 5.0f;
    private const float MAX_IFR_TIME = 40.0f;
    private const float MAX_FFR_TIME = 40.0f;

    void Start()
    {
        UnityEPL.SetExperimentName("VFFR");
        LoadWords();
        LoadNumberingPool();
        StartCoroutine(RunExperiment());
    }

    private void LoadNumberingPool()
    {
        numberingWords = new Dictionary<string, int>();
        string[] numberingPool = GetWordpoolLines("wasnorm_wordpool", false);
        for (int i = 1; i <= numberingPool.Length; i++)
        {
            numberingWords.Add(numberingPool[i - 1], i);
        }
    }

    private int GetWordNumber(string word)
    {
        if (word[word.Length - 1] == ' ')
            word = word.Substring(0, word.Length - 1);
        if (numberingWords.ContainsKey(word))
        {
            Debug.Log(numberingWords[word]);
            return numberingWords[word];
        }
        else
        {
            Debug.Log("-1");
            return -1;
        }
    }

    private void LoadWords()
    {
        words = GetWordpoolLines("wordpool_en", true);
    }

    private IEnumerator RunExperiment()
    {
        textDisplayer.DisplayText("subject name prompt", "Please enter the subject name and then press enter.");
        yield return new WaitForSeconds(3f);

        textDisplayer.ClearText();
        Debug.Log(inputField.IsActive());

        inputField.gameObject.SetActive(true);
        inputField.Select();
        do
        {
           yield return null;
           while (!Input.GetKeyDown(KeyCode.Return))
               yield return null;
        }

        while (!inputField.text.Equals("TEST") && (inputField.text.Length != 6 || 
                                                  !inputField.text[0].Equals('L') || 
                                                  !inputField.text[1].Equals('T') || 
                                                  !inputField.text[2].Equals('P') || 
                                                  !char.IsDigit(inputField.text[3]) || 
                                                  !char.IsDigit(inputField.text[4]) || 
                                                  !char.IsDigit(inputField.text[5])));

        UnityEPL.AddParticipant(inputField.text);
        SetSessionNumber();
        inputField.gameObject.SetActive(false);
        Cursor.visible = false;

        yield return DoMicrophoneTest();

        // Initial Free Recall
        if(UnityEPL.GetSessionNumber() >= 5) {
            yield return ShowRecallInstructions(); 

            // do initial free recall
            yield return DoInitialRecall();

        }

        yield return WriteBatchLstFiles();

        // TODO: why 10?
        for (int i = 0; i < 10; i++)
        {
            yield return PerformTrial(words, i, false);
        }

        // Final Free Recall
        if(UnityEPL.GetSessionNumber() >= 10) {

            //show prompt, start recall period
            yield return DoFinalRecall();
        }

        // over
        textDisplayer.DisplayText("end message", "Yay, the session is over!");
    }

    private IEnumerator DoCountdown()
    {
        for (int i = 10; i > 0; i--)
        {
            textDisplayer.DisplayText("countdown", i.ToString());
            yield return new WaitForSeconds(1);
            textDisplayer.ClearText();
        }
    }

    private IEnumerator ShowRecallInstructions() {
        yield return PressAnyKey(FIRST_INITIAL_FREE_RECALL_MESSAGE, new KeyCode[] { KeyCode.Return }, fullscreenTextDisplayer);
        yield return PressAnyKey(SECOND_INITIAL_FREE_RECALL_MESSAGE, new KeyCode[] { KeyCode.Return }, fullscreenTextDisplayer);

        if(UnityEPL.GetSessionNumber() >= 10) {
            yield return PressAnyKey(FIRST_FINAL_FREE_RECALL_MESSAGE, new KeyCode[] { KeyCode.Return }, fullscreenTextDisplayer);
            yield return PressAnyKey(SECOND_FINAL_FREE_RECALL_MESSAGE, new KeyCode[] { KeyCode.Return }, fullscreenTextDisplayer);
        }
    }

    private IEnumerator WaitForRecalTimeout(float min_time, float max_time, float timeout, float sleep=5.0f) {
        float MIN_RECALL_TIME = 30.0f; // minutes

        float recallStartTime = Time.time;
        float lastSpokenTime = Time.time;
        while((Time.time < recallStartTime + min_time 
                || Time.time - lastSpokenTime < timeout)
                && Time.time < recallStartTime + max_time)
        {
            if (voiceActivityDetection.SomeoneIsTalking())
            {
                lastSpokenTime = Time.time;
            }
            yield return new WaitForSeconds(sleep);
        }
    }

    private IEnumerator DoFinalRecall()
    {
        //final recall
        string wav_path = System.IO.Path.Combine(UnityEPL.GetDataPath(), "ffr.wav");
        soundRecorder.StartRecording();
        scriptedEventReporter.ReportScriptedEvent("final recall start", new Dictionary<string, object>());
        textDisplayer.DisplayText("final recall prompt", "******");

        yield return WaitForRecalTimeout(MIN_FFR_TIME, MAX_FFR_TIME, FFR_TIMEOUT);

        textDisplayer.ClearText();
        scriptedEventReporter.ReportScriptedEvent("final recall stop", new Dictionary<string, object>());
        soundRecorder.StopRecording(wav_path);
    }

    private IEnumerator DoInitialRecall()
    {
        //final recall
        string wav_path = System.IO.Path.Combine(UnityEPL.GetDataPath(), "ifr.wav");
        soundRecorder.StartRecording();
        scriptedEventReporter.ReportScriptedEvent("initial recall start", new Dictionary<string, object>());
        textDisplayer.DisplayText("initial recall prompt", "******");
        
        yield return WaitForRecalTimeout(MIN_IFR_TIME, MAX_IFR_TIME, IFR_TIMEOUT);

        textDisplayer.ClearText();
        scriptedEventReporter.ReportScriptedEvent("final recall stop", new Dictionary<string, object>());
        soundRecorder.StopRecording(wav_path);
    }
       
    private IEnumerator CreateLanguageModel(string lst_path)
    {
        string[] split_list = lst_path.Split('\\');
        string file_base = split_list[split_list.Length - 1];
        string kaldi_dir = file_base.Split('.')[0];
        string arpa_path = lst_path.Split('.')[0]+".arpa";
        string tmp_path = lst_path.Split('.')[0];
        yield return CreateKenLM(tmp_path, lst_path);
        yield return CreateKaldiLM(kaldi_dir, "/Users/francob/automatic_annotation/wordpool_files/wordpool.dict", arpa_path);
    }

    private IEnumerator CreateKenLM(string tmp_file, string lst_path)
    {
        System.Diagnostics.Process kenlm = new System.Diagnostics.Process();
        kenlm.StartInfo.FileName = "/bin/sh";
        kenlm.StartInfo.Arguments = string.Format("/Users/francob/kenlm.sh {0} {1}", tmp_file, lst_path);
        kenlm.StartInfo.UseShellExecute = true;
        kenlm.Start();
        kenlm.WaitForExit();
        yield return null;
    }

    private IEnumerator CreateKaldiLM(string no_ext, string wordpool, string arpa_path)
    {
        System.Diagnostics.Process kenlm = new System.Diagnostics.Process();
        kenlm.StartInfo.FileName = "/bin/sh";
        kenlm.StartInfo.Arguments = string.Format("/Users/francob/automatic_annotation/shell_scripts/create_lm.sh {0} {1} {2}", no_ext, wordpool, arpa_path);
        kenlm.StartInfo.UseShellExecute = true;
        kenlm.Start();
        kenlm.WaitForExit();
        yield return null;
    }

    private IEnumerator KaldiSpeechRecognition(string no_ext, string wav_path, string tmp_path)
    {
        System.Diagnostics.Process kenlm = new System.Diagnostics.Process();
        kenlm.StartInfo.FileName = "/bin/sh";
        kenlm.StartInfo.Arguments = string.Format("/Users/francob/automatic_annotation/shell_scripts/kaldi_speech_recognition.sh {0} {1} {2}", no_ext, wav_path, tmp_path+ ".ann");
        kenlm.StartInfo.UseShellExecute = true;
        kenlm.Start();
        kenlm.WaitForExit();
        yield return null;
    }



    private IEnumerator WriteBatchLstFiles()
    {
        for (int i = 0; i < 10; i++)
        {
            //write .lst
            string lst_path = System.IO.Path.Combine(UnityEPL.GetDataPath(), i.ToString() + ".lst");
            Debug.Log(lst_path);
            WriteAllLinesNoExtraNewline(lst_path, words[i]);
            yield return CreateLanguageModel(lst_path);
        }
        yield return null;
    }

    protected IEnumerator DoMicrophoneTest()
    {
        microphoneTestMessage.SetActive(true);
        bool repeat = false;
        string wavFilePath;

        do
        {
            yield return PressAnyKey("Press the spacebar to record a sound after the beep.", new KeyCode[]{KeyCode.Space}, textDisplayer);
            lowBeep.Play();
            textDisplayer.DisplayText("microphone test recording", "Recording...");
            textDisplayer.ChangeColor(Color.red);
            yield return new WaitForSeconds(lowBeep.clip.length);

            wavFilePath = System.IO.Path.Combine(UnityEPL.GetDataPath(), "microphone_test_" + DataReporter.RealWorldTime().ToString("yyyy-MM-dd_HH_mm_ss") + ".wav");
            soundRecorder.StartRecording();
            yield return new WaitForSeconds(5f);

            soundRecorder.StopRecording(wavFilePath);
            textDisplayer.ClearText();

            yield return new WaitForSeconds(1f);

            textDisplayer.DisplayText("microphone test playing", "Playing...");
            textDisplayer.ChangeColor(Color.green);
            audioPlayback.clip = soundRecorder.AudioClipFromDatapath(wavFilePath);
            audioPlayback.Play();
            yield return new WaitForSeconds(5f);
            textDisplayer.ClearText();
            textDisplayer.OriginalColor();

            textDisplayer.DisplayText("microphone test confirmation", "Did you hear the recording? \n(Y=Continue / N=Try Again / C=Cancel).");
            while (!Input.GetKeyDown(KeyCode.Y) && !Input.GetKeyDown(KeyCode.N) && !Input.GetKeyDown(KeyCode.C))
            {
                yield return null;
            }
            textDisplayer.ClearText();

            if (Input.GetKey(KeyCode.C))
                Quit();
            repeat = Input.GetKey(KeyCode.N);
        }
        while (repeat);

        if (!System.IO.File.Exists(wavFilePath))
            yield return PressAnyKey("WARNING: Wav output file not detected.  Sounds may not be successfully recorded to disk.", new KeyCode[] { KeyCode.Return }, textDisplayer);

        microphoneTestMessage.SetActive(false);
    }

    protected IEnumerator PressAnyKey(string displayText, KeyCode[] keyCodes, TextDisplayer pressAnyTextDisplayer)
    {
        yield return null;
        pressAnyTextDisplayer.DisplayText("press any key prompt", displayText);
        Dictionary<KeyCode, bool> keysPressed = new Dictionary<KeyCode, bool>();
        foreach (KeyCode keycode in keyCodes)
            keysPressed.Add(keycode, false);
        while (true)
        {
            yield return null;
            foreach (KeyCode keyCode in keyCodes)
            {
                if (Input.GetKeyDown(keyCode))
                    keysPressed[keyCode] = true;
                if (Input.GetKeyUp(keyCode))
                    keysPressed[keyCode] = false;
            }
            bool done = true;
            foreach (bool pressed in keysPressed.Values)
            {
                if (!pressed)
                    done = false;
            }
            if (done)
                break;
        }
        pressAnyTextDisplayer.ClearText();
    }

    private IEnumerator PerformTrial(string[] trial_words, int word_index, bool practice)
    {
        float FIRST_ISI_MIN = 1.0f;
        float FIRST_ISI_MAX = 1.6f;
        float STIMULUS_DISPLAY_LENGTH_MIN = 1.2f;
        float STIMULUS_DISPLAY_LENGTH_MAX = 1.8f;
        float RECALL_WAIT_LENGTH = 1f;
        float RECALL_MAIN_LENGTH = 2f;
        float RECALL_EXTRA_LENGTH = 0.5f;

        //isi
        yield return new WaitForSeconds(Random.Range(FIRST_ISI_MIN, FIRST_ISI_MAX));

        //stimulus
        string stimulus = trial_words[word_index];
        Debug.Log(stimulus);
        scriptedEventReporter.ReportScriptedEvent("stimulus", new Dictionary<string, object> () { { "word", stimulus }, { "index", word_index }, {"ltp word number", GetWordNumber(stimulus)}, { "practice", practice } });
        textDisplayer.DisplayText("stimulus display", stimulus);
        yield return new WaitForSeconds(Random.Range(STIMULUS_DISPLAY_LENGTH_MIN, STIMULUS_DISPLAY_LENGTH_MAX));
        scriptedEventReporter.ReportScriptedEvent("stimulus cleared", new Dictionary<string, object>() { { "word", stimulus }, { "index", word_index } });
        textDisplayer.ClearText();

        //recall
        soundRecorder.StartRecording();
        scriptedEventReporter.ReportScriptedEvent("recall start", new Dictionary<string, object>() { { "word", stimulus }, { "index", word_index } });
        float recallStartTime = Time.time;
        float lastSpokenTime = Time.time;
        bool someoneHasSpoken = false;
        bool badTrial = false;
        while (Time.time < recallStartTime + RECALL_WAIT_LENGTH)
        {
            if (voiceActivityDetection.SomeoneIsTalking())
            {
                someoneHasSpoken = true;
                tooSoonWarning.SetActive(true);
                lastSpokenTime = Time.time;
            }
            yield return null;
        }
        if (someoneHasSpoken)
            badTrial = true;
        
        while (!someoneHasSpoken || voiceActivityDetection.SomeoneIsTalking() || Time.time < recallStartTime + RECALL_WAIT_LENGTH + RECALL_MAIN_LENGTH || Time.time < lastSpokenTime + RECALL_EXTRA_LENGTH)
        {
            if (voiceActivityDetection.SomeoneIsTalking())
            {
                someoneHasSpoken = true;
                lastSpokenTime = Time.time;
            }
            yield return null;
        }
        scriptedEventReporter.ReportScriptedEvent("recall stop", new Dictionary<string, object>() { { "word", stimulus }, { "index", word_index }, { "too_fast", badTrial} });


        //stop recording and write .wav
        string wav_path = System.IO.Path.Combine(UnityEPL.GetDataPath(), word_index.ToString() + ".wav");
        if (practice)
            wav_path = System.IO.Path.Combine(UnityEPL.GetDataPath(), word_index.ToString() + "_practice.wav");
        soundRecorder.StopRecording(wav_path);

        //beep
        scriptedEventReporter.ReportScriptedEvent("beep start", new Dictionary<string, object>());
        lowBeep.Play();
        yield return new WaitForSeconds(lowBeep.clip.length);
        scriptedEventReporter.ReportScriptedEvent("beep stop", new Dictionary<string, object>());

        tooSoonWarning.SetActive(false);

        string[] split_list = wav_path.Split('\\');
        string file_base = split_list[split_list.Length - 1];
        string kaldi_dir = file_base.Split('.')[0];
        string lst_path = wav_path.Split('.')[0] + ".lst";
        string tmp_path = wav_path.Split('.')[0];
        yield return KaldiSpeechRecognition(kaldi_dir, wav_path, tmp_path);

    }

    private string[] GetWordpoolLines(string path, bool shuffle)
    {
        string text = Resources.Load<TextAsset>(path).text;
        string[] lines = text.Split(new[] { '\r', '\n' });

        if (shuffle)
            Shuffle<string>(new System.Random(), lines);

        return lines;
    }

    //thanks Matt Howells
    private void Shuffle<T> (System.Random rng, T[] array)
    {
        int n = array.Length;
        while (n > 1) 
        {
            int k = rng.Next(n--);
            T temp = array[n];
            array[n] = array[k];
            array[k] = temp;
        }
    }

    //thanks Virtlink from stackoverflow
    protected static void WriteAllLinesNoExtraNewline(string path, params string[] lines)
    {
        if (path == null)
            throw new UnityException("path argument should not be null");
        if (lines == null)
            throw new UnityException("lines argument should not be null");

        using (var stream = System.IO.File.OpenWrite(path))
        {
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(stream))
            {
                if (lines.Length > 0)
                {
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        writer.WriteLine(lines[i]);
                    }
                    writer.Write(lines[lines.Length - 1]);
                }
            }
        }
    }

    private void SetSessionNumber()
    {
        int nextSessionNumber = 0;
        UnityEPL.SetSessionNumber(0);
        while (System.IO.Directory.Exists(UnityEPL.GetDataPath()))
        {
            nextSessionNumber++;
            UnityEPL.SetSessionNumber(nextSessionNumber);
        }
    }

    private void Quit()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
