using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;
using System;

public class AvatarExperiment : MonoBehaviour
{
    public int PROGRESSACTIONS; // number of actions (button presses) a user has to perform in order to progress through the study
    public ProgressBar progressBar;
    public PlayerPosition playerPosition;
    public GameObject buttonLeftGO, buttonRightGO, buttonNextGO, buttonBeginGO, buttonEndGO, buttonHeightGO; // physical buttons
    private PhysicsButton btnL, btnR, btnN, btnBegin, btnEnd, btnHeight;

    public PlayerRaycasting raycasting;
    public RecorderReplayer recRep;
    public MeasurementsUploader uploader;
    public Logger logger;

    //public string recordingsDir;
    public Canvas promptCanvas;
    public Text promptText; // any text that prompts the user to do something

    public Canvas canvas;
    public GameObject currentPanel;
    public GameObject introductionPanel;
    public GameObject heightAdjustmentPanel;
    public GameObject selectionPanel;
    public Text firstButtonText; // on selection panel
    public Text secondButtonText;
    public Text selectionPromptText;
    public GameObject questionPanel;
    public Text questionText; // for the question panels
    public Button nextButton; // next button after questions
    public Button questionnaireNextButton;
    public GameObject questionnairePanel;
    public GameObject revelationPanel;
    public GameObject finishPanel;
    public Button finishButton;
    public Text resultsUploadedText;

    public List<GameObject> questionSets; // different panels with various questions for different stages of the experiment

    public int currentQuestionSet = 1; // set 1

    private float waitForSeconds = 3.0f;
    private bool consentGiven = false;
    private bool adjustHeight = false;
    private bool responseSelected = false;
    private bool gotoNextPanel = false;
    private bool allQuestionsAnswered = false;
    private int maxFrame = 0;
    private bool replayLoaded = false;
    private bool replayCleanedUp = false;

    //private string q1 = "Distraction"; // actually this is "attention" now
    private string q2 = "Gender";
    private string q3 = "Age";
    private string q4 = "TimeVideoGames";
    private string q5 = "TimeVR";
    //private string q6 = "Feedback";

    private string[] qSet2;

    private string ParticipantID = null;
    //private string questionnaireURL = "https://docs.google.com/forms/d/e/1FAIpQLSelJbjGHvJC5vcTe7NKnQOm6v-kpO7TGvLXHx_hkGO8J_9MdA/viewform?usp=pp_url&entry.1804148172=";
    //private string questionnaireMidStudyURL = "https://docs.google.com/forms/d/e/1FAIpQLSelJbjGHvJC5vcTe7NKnQOm6v-kpO7TGvLXHx_hkGO8J_9MdA/viewform?usp=pp_url&entry.1804148172=";

    // 24 videos
    private string[] filesFirstRound = 
        new string[]
        {
            "rec01r1", "rec01n",
            //"rec02r2", "rec02b",
            //"rec03n", "rec03r2",
            //"rec04b", "rec04r1",
            //"rec05r2", "rec05n",
            //"rec06b", "rec06r2",
            //"rec02n", "rec02r1",
            //"rec03r1", "rec03b",
            //"rec04n", "rec04r2",
            //"rec05b", "rec05r1",
            //"rec06r1", "rec06n",
            //"rec01r2", "rec01b",
        };

    private string[] filesSecondRound = new string[]
        {
            "rec01b","rec02r1",
            //"rec03r1","rec04n",
            //"rec05r2","rec02b",
            //"rec06n","rec03r2",
            //"rec05r1","rec04b",
            //"rec06r2","rec02n",
            //"rec05b","rec01r2",
            //"rec03n","rec04r1",
            //"rec02r2","rec06b",
            //"rec01n","rec04r2",
            //"rec03b","rec01r1",
            //"rec06r1","rec05n"
        };



    private Queue<string> queueFirstRound;
    private Queue<string> queueSecondRound;

    private float fadeTime = 2.0f;
    private int ROUND = 1; // experiment consists of 2 rounds, in first round participants don't know about record and replay
    private string watchFirstVid = "Watch the 1st recording!";
    private string watchSecondVid = "Watch the 2nd recording!";
    private string watchMixedVid = "Watch the recording!";
    private string secondRoundSelectionText = "<b>Were there one or two actors in the recording you just saw?</b>";

    private string questionPrompt = "<b>Answer the following questions:</b>";

    private string firstQuestion = "<b>1.) What were your deciding factors when you chose one recording over the other? \n" +
        "2.) Why did you prefer one recording over the other?</b>";

    private string secondQuestion = "<b> 1.) What were your deciding factors when you selected the number of actors? \n" +
        "<color=red>2.) What did you look for in the recordings to figure out if they were recorded by one person?</color></b>";

    private string headsetOffPromptWithID;

    private ExperimentData experimentData;
    private UserResponse userResponse;
    //private string questionResponse = "";

    [System.Serializable]
    public class ExperimentData
    {
        public string participantID;
        public PlayerPosition.Distances distance;
        public List<UserResponse> videoResponses = new List<UserResponse>();
        public Questionnaire questions = new Questionnaire();
        public List<EventRecord> eventRecords = new List<EventRecord>();
    }

    [System.Serializable]
    public class UserResponse
    {
        public int round;
        public int userSelection; // lists which video was selected by the participant (either first '1' or second '2', in round 2 '1' means one actor and '2' means two actors)!!!
        public string firstVideo; 
        public string secondVideo; // in round 2 this is empty because we only have one video per user response
        public PlayerRaycasting.RaycastInfo raycastInfo1; // for first replay in Round 1 and replay in Round 2
        public PlayerRaycasting.RaycastInfo raycastInfo2; // for second replay in Round 1 (empty in Round 2)

        public UserResponse(int round, int userSelection, string firstVideo, string secondVideo, PlayerRaycasting.RaycastInfo raycastInfo1, PlayerRaycasting.RaycastInfo raycastInfo2)
        {
            this.round = round;
            this.userSelection = userSelection;
            this.firstVideo = firstVideo;
            this.secondVideo = secondVideo;
            this.raycastInfo1 = raycastInfo1;
            this.raycastInfo2 = raycastInfo2; // is empty in Round 2
        }
    }

    [System.Serializable]
    public class Questionnaire
    {
        public string qRound1;
        public string qRound2;
        public string distractionSpeak;
        public string distractionGender;
        public string distractionCap;
        public string distractionSameActor;
        public string distractionSize;
        public string gender;
        public string age;
        public string gameTime;
        public string vrTime;
        public string about; // what do they think the study is about
        public string feedback;
    }

    void Start()
    {
        qSet2 = new string[] { q2, q3, q4, q5 }; // questions in set 2
        // first round is answered in pairs, second round individually + then 2 open text questions after each round + 3 questionnaire sets (attention, demographics, feedback)
        PROGRESSACTIONS = filesFirstRound.Length / 2 + filesSecondRound.Length; // without questions as they will be on PC

        ParticipantID = DateTime.Now.ToString("HH-mm-ss_dd-MM-yyyy");
        var shortID = ParticipantID.Split('_');
        //questionText.text = "<b>Please take off the headset to answer questions about the videos you just watched! Remember your ID: </b>" + 
        //    string.Format("<b><color=red>{0}</color></b>", shortID[0]);
        questionText.text = $"<b>Please take off the headset to answer questions about the videos you just watched! Remember your ID: <color=red>{shortID[0]}</color></b>";

        Debug.Log("Participant ID: " + ParticipantID);
        queueFirstRound = new Queue<string>(filesFirstRound);
        queueSecondRound = new Queue<string>(filesSecondRound);

        btnL = buttonLeftGO.GetComponentInChildren<PhysicsButton>();
        btnR = buttonRightGO.GetComponentInChildren<PhysicsButton>();
        btnN = buttonNextGO.GetComponentInChildren<PhysicsButton>();
        btnBegin = buttonBeginGO.GetComponentInChildren<PhysicsButton>();
        btnEnd = buttonEndGO.GetComponentInChildren<PhysicsButton>();
        btnHeight = buttonHeightGO.GetComponentInChildren<PhysicsButton>();

        //Debug.Log(btnL + " " + btnR + " " + btnN + " " +  btnBegin + " " + btnEnd + " " + btnHeight);

        // do this in RecorderReplayer itself!
        //recRep.path = Path.Combine(Application.streamingAssetsPath, "recordings");
        //Debug.Log("Overwrite RecorderReplayer path to recordings: " + recRep.path);

        experimentData = new ExperimentData();

        experimentData.distance = playerPosition.distanceFromReplays; // log which distance this user had from the replays
        experimentData.participantID = ParticipantID;
        // the response is not known here but we will set it once we do 
        uploader.OnUploadSuccessful += Uploader_OnUploadSuccessful;
        recRep.replayer.OnReplayLoaded += Replayer_OnReplayLoaded;
        recRep.replayer.OnReplayCleanedUp += Replayer_OnReplayCleanedUp;
        StartCoroutine(StudyProcedureCoroutine());
    }

    private void Replayer_OnReplayCleanedUp(object sender, EventArgs e)
    {
        replayCleanedUp = true;
        Debug.Log("Coroutine: Replay cleaned up!");

    }

    private void Replayer_OnReplayLoaded(object sender, EventArgs e)
    {
        replayLoaded = true;
        maxFrame = recRep.replayer.recInfo.frames;
        Debug.Log("Coroutine: Replay loaded!");
    }

    // on consent button press
    public void GiveConsent()
    {
        consentGiven = true;
        Debug.Log("Coroutine: Consent given!");

    }
    // on adjust height button press
    public void AdjustHeight()
    {
        adjustHeight = true;
        Debug.Log("Coroutine: Height adjusted!");

    }

    public IEnumerator LoadReplay(string fileName)
    {
        Debug.Log("Load replay: " + fileName);
        recRep.menuRecRep.SelectReplayFile(fileName);
        recRep.menuRecRep.ToggleReplay();

        yield return new WaitUntil(() => recRep.replayer.recInfo != null);

    }
    public void DisableCanvas()
    {
        canvas.enabled = false;
        Debug.Log("Coroutine: Canvas disabled!");

    }
    public void EnableCnavas()
    {
        canvas.enabled = true;
        Debug.Log("Coroutine: Canvas enabled!");

    }
    public void NextButtonPressed()
    {
        gotoNextPanel = true;
        Debug.Log("Coroutine: 'Next' button pressed!");

    }
    public void ResponseSelected(Button btn)
    {
        // log user selection
        if (btn.tag == "First")
        {
            Debug.Log("First button selected!");
            userResponse.userSelection = 1;
        }
        else // Second
        {
            Debug.Log("Second button selected!");
            userResponse.userSelection = 2;
        }
        responseSelected = true;
    }

    public void ResponseButtonPressed(PhysicsButton.Buttons button)
    {
        switch(button)
        {
            case PhysicsButton.Buttons.LeftButton:
                Debug.Log("First button selected!");
                userResponse.userSelection = 1;
                break;
            case PhysicsButton.Buttons.RightButton:
                Debug.Log("Second button selected!");
                userResponse.userSelection = 2;
                break;
        }
        responseSelected = true;
    }

    public IEnumerator StudyProcedureCoroutine()
    {
        ROUND = 1;
        // first panel is consent panel, we wait until consent is given then we can switch to height adjustment panel
        //yield return new WaitUntil(() => consentGiven);
        //SwitchPanel(heightAdjustmentPanel);
        buttonHeightGO.SetActive(true);
        yield return new WaitUntil(() => adjustHeight);
        AvatarHeightAdjustment aha = GetComponent<AvatarHeightAdjustment>();
        yield return aha.WaitTakeMeasurementAndFade(2, 3);
        btnHeight.ResetButtonPress();
        buttonHeightGO.SetActive(false);
        SwitchPanel(introductionPanel);
        playerPosition.UpdateUIPosition(); // make it a bit higher than before height adjustment
        buttonBeginGO.SetActive(true);
        // when pressing 'Begin button' canvas gets disabled

        yield return new WaitUntil(() => !canvas.enabled);
        logger.LogVideoPanelSwitch(); // i know there are no videos, but it is basically the same
        yield return new WaitForSeconds(1);
        btnBegin.ResetButtonPress();
        buttonBeginGO.SetActive(false);

        ///////////////////////////////////////////
        // ROUND 1
        ///////////////////////////////////////////
        while (queueFirstRound.Count > 0)
        {
            if (canvas.enabled)
                DisableCanvas();
            // FIRST REPLAY
            var texPos = raycasting.Dequeue(1);
            var firstReplay = queueFirstRound.Dequeue();
            yield return LoadReplay(firstReplay);
            yield return new WaitUntil(() => replayLoaded);
            replayLoaded = false;
            promptText.text = watchFirstVid;
            yield return ShowTextForSeconds(fadeTime, promptText);//yield return FadeText(fadeTime, promptText);
            raycasting.RaycastingEnabled(true);
            recRep.menuRecRep.PlayPauseReplay();
            // wait for the replay to be played almost to the end pause and delete the replay
            yield return new WaitUntil(() => recRep.currentReplayFrame >= maxFrame - 10);
            var raycastInfo1 = raycasting.GetRaycastInfo();
            raycasting.RaycastingEnabled(false);
            recRep.menuRecRep.PlayPauseReplay();
            yield return new WaitForSeconds(0.5f);
            recRep.menuRecRep.ToggleReplay();
            yield return new WaitUntil(() => replayCleanedUp);
            // SECOND REPLAY: load second replay, show text, and play
            var secondReplay = queueFirstRound.Dequeue();
            yield return LoadReplay(secondReplay);
            yield return new WaitUntil(() => replayLoaded);
            replayLoaded = false;
            promptText.text = watchSecondVid;
            yield return ShowTextForSeconds(fadeTime, promptText);//yield return FadeText(fadeTime, promptText);
            raycasting.RaycastingEnabled(true);
            recRep.menuRecRep.PlayPauseReplay();
            yield return new WaitUntil(() => recRep.currentReplayFrame >= maxFrame - 10);
            var raycastInfo2 = raycasting.GetRaycastInfo();
            raycasting.RaycastingEnabled(false);
            recRep.menuRecRep.PlayPauseReplay();
            yield return new WaitForSeconds(0.5f);
            recRep.menuRecRep.ToggleReplay();
            yield return new WaitUntil(() => replayCleanedUp);

            SwitchPanel(selectionPanel);
            EnableCnavas();
            buttonLeftGO.SetActive(true);
            buttonRightGO.SetActive(true);
            logger.LogSelectionPanelSwitch();
            progressBar.UpdateProgress();

            raycastInfo1.texPos = texPos;
            raycastInfo2.texPos = texPos;
            userResponse = new UserResponse(ROUND, -1, firstReplay, secondReplay, raycastInfo1, raycastInfo2);
            yield return new WaitUntil(() => responseSelected);
            responseSelected = false;
            Debug.Log("user response: " + JsonUtility.ToJson(userResponse));
            experimentData.videoResponses.Add(userResponse);

            yield return new WaitForSeconds(1);
            btnL.ResetButtonPress();
            btnR.ResetButtonPress();
            buttonLeftGO.SetActive(false);
            buttonRightGO.SetActive(false);
        }

        SwitchPanel(questionPanel); // question screen
        progressBar.UpdateProgress();
        logger.LogQuestionPanelSwitch(ROUND);
        yield return new WaitForSeconds(waitForSeconds); // wait for 1 minute until button gets enabled! so participants cannot accidentaly continue
        nextButton.interactable = true;
        buttonNextGO.SetActive(true);
        yield return new WaitUntil(() => gotoNextPanel);
        //SwitchPanel(questionnairePanel); // set 1: attention check panel
        //progressBar.UpdateProgress();
        //logger.LogQuestionnairePanelSwitch();
        //yield return new WaitUntil(() => allQuestionsAnswered); // panel switch to revelation panel automaticaly
        yield return new WaitForSeconds(2);
        btnN.ResetButtonPress();
        buttonNextGO.SetActive(false);
        SwitchPanel(revelationPanel);
        yield return new WaitForSeconds(1);
        buttonBeginGO.SetActive(true);

        firstButtonText.text = "1 actor";
        secondButtonText.text = "2 actors";
        selectionPromptText.text = secondRoundSelectionText;
        questionText.text = secondQuestion; // for interview questions after ROUND 2
        ROUND = 2;
        gotoNextPanel = false;
        nextButton.interactable = false;
        // START ROUND 2 and DISABLE CANVAS ON BUTTON CLICK
        yield return new WaitUntil(() => !canvas.enabled);
        logger.LogVideoPanelSwitch();
        yield return new WaitForSeconds(2);
        btnBegin.ResetButtonPress();
        buttonBeginGO.SetActive(false);

        while (queueSecondRound.Count > 0)
        {
            if (canvas.enabled)
                DisableCanvas();
            var replay = queueSecondRound.Dequeue();
            var texPos = raycasting.Dequeue(2);
            yield return LoadReplay(replay);
            yield return new WaitUntil(() => replayLoaded);
            replayLoaded = false;
            promptText.text = watchMixedVid;
            yield return ShowTextForSeconds(fadeTime, promptText);//yield return FadeText(fadeTime, promptText);
            raycasting.RaycastingEnabled(true);
            recRep.menuRecRep.PlayPauseReplay();
            // wait for the replay to be played almost to the end pause and delete the replay
            yield return new WaitUntil(() => recRep.currentReplayFrame >= maxFrame - 10);
            var raycastInfo1 = raycasting.GetRaycastInfo();
            raycasting.RaycastingEnabled(false);
            recRep.menuRecRep.PlayPauseReplay();
            yield return new WaitForSeconds(0.5f);
            recRep.menuRecRep.ToggleReplay();
            yield return new WaitUntil(() => replayCleanedUp);

            SwitchPanel(selectionPanel);
            EnableCnavas();
            buttonLeftGO.SetActive(true);
            buttonRightGO.SetActive(true);
            progressBar.UpdateProgress();
            logger.LogSelectionPanelSwitch();
            raycastInfo1.texPos = texPos;
            userResponse = new UserResponse(ROUND, -1, replay, "", raycastInfo1, null); // second list is empty because we only have one replay
            yield return new WaitUntil(() => responseSelected);
            responseSelected = false;
            experimentData.videoResponses.Add(userResponse);
            yield return new WaitForSeconds(1);
            btnL.ResetButtonPress();
            btnR.ResetButtonPress();
            buttonLeftGO.SetActive(false);
            buttonRightGO.SetActive(false);
        }

        //SwitchPanel(questionPanel); // question screen
        //progressBar.UpdateProgress();
        //logger.LogQuestionPanelSwitch(ROUND);
        //yield return new WaitForSeconds(waitForSeconds/3); // wait for 20 seconds until button gets enabled! so participants cannot accidentaly continue
        //nextButton.interactable = true;
        //yield return new WaitUntil(() => gotoNextPanel);
        //gotoNextPanel = false;
        //logger.LogQuestionnairePanelSwitch();
        //SwitchPanel(questionnairePanel); // set 2: gender, age, game, vr questions
        // questionnaires (set 2 and 3)
        //progressBar.UpdateProgress();
        //yield return new WaitUntil(() => allQuestionsAnswered); // switches automatically to last set with about and feedback question
        //logger.LogQuestionnairePanelSwitch();
        //yield return new WaitForSeconds(waitForSeconds); // wait for 1 minute until button gets enabled! so participants cannot accidentaly continue

        // end and data upload
        //yield return new WaitUntil(() => gotoNextPanel);
        // log finish panel switch and get event records for uploading
        logger.LogFinishPanelSwitch();
        experimentData.eventRecords = logger.GetEventRecords();

        Color opaque = Color.red;
        resultsUploadedText.color = opaque;
        resultsUploadedText.text = "Uploading in progress! Do NOT quit!";
        // send all the data to the server AND also store it locally (just in case)
        File.WriteAllText(Application.persistentDataPath + "/ParticipantData/" + ParticipantID + ".json", JsonUtility.ToJson(experimentData, true));
        uploader.Send(experimentData);

        progressBar.UpdateProgress();
        SwitchPanel(finishPanel);
        buttonEndGO.SetActive(true);

        yield return null;
    }

    // starts with active set 1
    public void SwitchFromQuestionnairePanel()
    {
        if (currentQuestionSet == 1)
        {
            // distraction question
            ToggleGroup[] toggleGroups = questionSets[currentQuestionSet - 1].GetComponentsInChildren<ToggleGroup>();
            bool allAnswered = true;
            foreach (var tg in toggleGroups)
            {
                allAnswered = allAnswered && tg.AnyTogglesOn();
            }

            if (allAnswered)
            {
                progressBar.UpdateProgress();
                Debug.Log("All questions answered!");
                foreach (var group in toggleGroups)
                {
                    var activeToggle = group.GetFirstActiveToggle();

                    switch (group.name)
                    {
                        case "LikertScale S1":
                            experimentData.questions.distractionSpeak = activeToggle.name; // 1 - 7 corresponds to likert scale responses
                            break;
                        case "LikertScale S2":
                            experimentData.questions.distractionGender = activeToggle.name;
                            break;
                        case "LikertScale S3":
                            experimentData.questions.distractionCap = activeToggle.name;
                            break;
                        case "LikertScale S4":
                            experimentData.questions.distractionSameActor = activeToggle.name;
                            break;
                        case "LikertScale S5":
                            experimentData.questions.distractionSize = activeToggle.name;
                            break;
                    }
                    Debug.Log("Set 2 Situation: " + group.name + ", Response: " + activeToggle.name);
                }
                questionSets[currentQuestionSet - 1].SetActive(false);
                currentQuestionSet++;
                questionSets[currentQuestionSet - 1].SetActive(true);
                allQuestionsAnswered = allAnswered;
                SwitchPanel(revelationPanel);
            }
            else
            {
                promptText.text = "Please answer all questions!";
                StartCoroutine(FadeText(2.0f, promptText));
                allQuestionsAnswered = false;
            }

        }
        else if (currentQuestionSet == 2)
        {
            ToggleGroup[] toggleGroups = questionSets[currentQuestionSet - 1].GetComponentsInChildren<ToggleGroup>();
            bool allAnswered = true;
            foreach (var tg in toggleGroups)
            {
                allAnswered = allAnswered && tg.AnyTogglesOn();
            }

            if (allAnswered)
            {
                var answers = new List<string>();
                progressBar.UpdateProgress();
                Debug.Log("All questions answered!");
                for (int i = 0; i < toggleGroups.Length; i++)
                {
                    var activeToggles = toggleGroups[i].ActiveToggles(); // there should always be only one toggle active
                    var answer = "";
                    foreach (var toggle in activeToggles)
                    {
                        answer += toggle.GetComponentInChildren<Text>().text; 
                        Debug.Log("Set 2 Question: " + qSet2[i] + ", Response: " + answer);
                    }
                    answers.Add(answer);
                }
                experimentData.questions.gender = answers[0];
                experimentData.questions.age = answers[1];
                experimentData.questions.gameTime = answers[2];
                experimentData.questions.vrTime = answers[3];

                // do not switch panel but set!
                questionSets[currentQuestionSet - 1].SetActive(false);
                currentQuestionSet++;
                questionSets[currentQuestionSet - 1].SetActive(true);
                questionnaireNextButton.interactable = false; // to assure that participants will answer the "About" question
                allQuestionsAnswered = true;
            }
            else
            {
                promptText.text = "Please answer all questions!";
                StartCoroutine(FadeText(2.0f, promptText));
                allQuestionsAnswered = false;
            }
        }
    }


    private void Uploader_OnUploadSuccessful(object sender, System.EventArgs e)
    {
        Debug.Log("Uploaded experiment data!");
        // show upload successful text 
        //Color c = resultsUploadedText.color;
        //Color opaque = new Color(c.r, c.g, c.b, 1);
        resultsUploadedText.text = "Experiment results uploaded successfully!";
        resultsUploadedText.color = Color.green;

        // enable finish button
        finishButton.interactable = true;
    }

    public void EndApplication()
    {
        Debug.Log("End Application!");
        Application.Quit();
    }

    public void SwitchPanel(GameObject newPanel)
    {

        if (currentPanel != newPanel)
        {
            currentPanel.SetActive(false);
        }

        newPanel.SetActive(true);
        currentPanel = newPanel;
    }
   
    public IEnumerator ShowTextForSeconds(float s, Text t)
    {
        promptCanvas.enabled = true;
        t.color = new Color(t.color.r, t.color.g, t.color.b, 1);
        yield return new WaitForSeconds(s);
        promptCanvas.enabled = false;
    }

    public IEnumerator FadeText(float t, Text i)
    {
        i.color = new Color(i.color.r, i.color.g, i.color.b, 1);
        while (i.color.a > 0.0f)
        {
            i.color = new Color(i.color.r, i.color.g, i.color.b, i.color.a - (Time.deltaTime / t));
            yield return null;
        }
    }
}
