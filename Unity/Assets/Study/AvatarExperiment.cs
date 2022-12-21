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

    public RecorderReplayer recRep;
    public MeasurementsUploader uploader;
    public Logger logger;

    //public string recordingsDir;
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

    private float fadeTime = 1.0f;
    private int ROUND = 1; // experiment consists of 2 rounds, in first round participants don't know about record and replay
    private string watchFirstVid = "<b>Watch the 1st recording!</b>";
    private string watchSecondVid = "<b>Watch the 2nd recording!</b>";
    private string watchMixedVid = "<b>Watch the recording!</b>";
    private string secondRoundSelectionText = "<b>Were there one or two actors in the recording you just saw?</b>";

    private string questionPrompt = "<b>Answer the following questions:</b>";

    private string firstQuestion = "<b>1.) What were your deciding factors when you chose one recording over the other? \n" +
        "2.) Why did you prefer one recording over the other?</b>";

    private string secondQuestion = "<b> 1.) What were your deciding factors when you selected the number of actors? \n" +
        "<color=red>2.) What did you look for in the recordings to figure out if they were recorded by one person?</color></b>";

    private ExperimentData experimentData;
    private UserResponse userResponse;
    //private string questionResponse = "";

    [System.Serializable]
    public class ExperimentData
    {
        public string participantID;
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

        public UserResponse(int round, int userSelection, string firstVideo, string secondVideo)
        {
            this.round = round;
            this.userSelection = userSelection;
            this.firstVideo = firstVideo;
            this.secondVideo = secondVideo;
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
    //[System.Serializable]
    //public class QuestionnaireResponse
    //{
    //    public string question;
    //    public string answer;
    //}

    void Start()
    {
        qSet2 = new string[] { q2, q3, q4, q5 }; // questions in set 2
        // first round is answered in pairs, second round individually + then 2 open text questions after each round + 3 questionnaire sets (attention, demographics, feedback)
        PROGRESSACTIONS = filesFirstRound.Length / 2 + filesSecondRound.Length + 2 + 3;

        ParticipantID = DateTime.Now.ToString("HH-mm-ss_dd/MM/YYYY");
        queueFirstRound = new Queue<string>(filesFirstRound);
        queueSecondRound = new Queue<string>(filesSecondRound);

        recRep.path = Path.Combine(Application.streamingAssetsPath, "recordings");
        Debug.Log("Overwrite RecorderReplayer path to recordings: " + recRep.path);

        experimentData = new ExperimentData();
        
        experimentData.participantID = ParticipantID;
        // the response is not known here but we will set it once we do 
        uploader.OnUploadSuccessful += Uploader_OnUploadSuccessful;
    }

    // on consent button press
    public void GiveConsent()
    {
        consentGiven = true;
    }
    // on adjust height button press
    public void AdjustHeight()
    {
        adjustHeight = true;
    }

    public IEnumerator LoadReplay(string fileName)
    {
        recRep.menuRecRep.SelectReplayFile(fileName);
        recRep.menuRecRep.ToggleReplay();

        yield return new WaitUntil(() => recRep.replayer.recInfo != null);
    }
    public void DisableCanvas()
    {
        canvas.enabled = false;
    }
    public void EnableCnavas()
    {
        canvas.enabled = true;
    }
    public void NextButtonPressed()
    {
        gotoNextPanel = true;
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
    }

    public IEnumerator StudyProcedureCoroutine()
    {
        ROUND = 1;
        // first panel is consent panel, we wait until consent is given then we can switch to height adjustment panel
        yield return new WaitUntil(() => consentGiven);
        SwitchPanel(heightAdjustmentPanel);
        yield return new WaitUntil(() => adjustHeight);
        AvatarHeightAdjustment aha = GetComponent<AvatarHeightAdjustment>();
        yield return aha.WaitTakeMeasurementAndFade(2, 3);
        SwitchPanel(introductionPanel);
        
        // when clicking 'Begin' canvas gets disabled
        yield return new WaitUntil(() => !canvas.enabled);
        logger.LogVideoPanelSwitch(); // i know there are no videos, but it is basically the same
        ///////////////////////////////////////////
        // ROUND 1
        ///////////////////////////////////////////
        while (queueFirstRound.Count > 0)
        {
            // FIRST REPLAY
            var firstReplay = queueFirstRound.Dequeue();
            LoadReplay(firstReplay);
            var maxFrame = recRep.replayer.recInfo.frames;
            promptText.text = watchFirstVid;
            FadeText(fadeTime, promptText);
            recRep.menuRecRep.PlayPauseReplay();
            // wait for the replay to be played almost to the end pause and delete the replay
            yield return new WaitUntil(() => recRep.currentReplayFrame >= maxFrame - 10);
            recRep.menuRecRep.PlayPauseReplay();
            recRep.menuRecRep.ToggleReplay();

            // SECOND REPLAY: load second replay, show text, and play
            var secondReplay = queueFirstRound.Dequeue();
            LoadReplay(secondReplay);
            maxFrame = recRep.replayer.recInfo.frames;
            promptText.text = watchSecondVid;
            FadeText(fadeTime, promptText);
            recRep.menuRecRep.PlayPauseReplay();
            yield return new WaitUntil(() => recRep.currentReplayFrame >= maxFrame - 10);
            recRep.menuRecRep.PlayPauseReplay();
            recRep.menuRecRep.ToggleReplay();

            EnableCnavas();
            SwitchPanel(selectionPanel);
            logger.LogSelectionPanelSwitch();
            progressBar.UpdateProgress();

            userResponse = new UserResponse(ROUND, -1, firstReplay, secondReplay);
            yield return new WaitUntil(() => responseSelected);
            responseSelected = false;
            experimentData.videoResponses.Add(userResponse);
        }

        SwitchPanel(questionPanel); // question screen
        progressBar.UpdateProgress();
        logger.LogQuestionPanelSwitch(ROUND);
        yield return new WaitForSeconds(waitForSeconds); // wait for 1 minute until button gets enabled! so participants cannot accidentaly continue
        nextButton.enabled = true;
        yield return new WaitUntil(() => gotoNextPanel);
        SwitchPanel(questionnairePanel); // set 1: attention check panel
        progressBar.UpdateProgress();
        logger.LogQuestionnairePanelSwitch();
        yield return new WaitUntil(() => allQuestionsAnswered); // panel switch to revelation panel automaticaly

        firstButtonText.text = "1 actor";
        secondButtonText.text = "2 actors";
        selectionPromptText.text = secondRoundSelectionText;
        questionText.text = secondQuestion; // for interview questions after ROUND 2
        ROUND = 2;
        gotoNextPanel = false;
        nextButton.enabled = false;
        // START ROUND 2 and DISABLE CANVAS ON BUTTON CLICK
        yield return new WaitUntil(() => !canvas.enabled);
        logger.LogVideoPanelSwitch();

        while(queueSecondRound.Count > 0)
        {
            var replay = queueFirstRound.Dequeue();
            LoadReplay(replay);
            var maxFrame = recRep.replayer.recInfo.frames;
            promptText.text = watchMixedVid;
            FadeText(fadeTime, promptText);
            recRep.menuRecRep.PlayPauseReplay();
            // wait for the replay to be played almost to the end pause and delete the replay
            yield return new WaitUntil(() => recRep.currentReplayFrame >= maxFrame - 10);
            recRep.menuRecRep.PlayPauseReplay();
            recRep.menuRecRep.ToggleReplay();

            EnableCnavas();
            SwitchPanel(selectionPanel);
            progressBar.UpdateProgress();
            logger.LogSelectionPanelSwitch();
            userResponse = new UserResponse(ROUND, -1, replay, "");
            yield return new WaitUntil(() => responseSelected);
            responseSelected = false;
            experimentData.videoResponses.Add(userResponse);
        }

        SwitchPanel(questionPanel); // question screen
        progressBar.UpdateProgress();
        logger.LogQuestionPanelSwitch(ROUND);
        yield return new WaitForSeconds(waitForSeconds); // wait for 1 minute until button gets enabled! so participants cannot accidentaly continue
        nextButton.enabled = true;
        yield return new WaitUntil(() => gotoNextPanel);
        gotoNextPanel = false;
        logger.LogQuestionnairePanelSwitch();
        SwitchPanel(questionnairePanel); // set 2: gender, age, game, vr questions
        // questionnaires (set 2 and 3)
        progressBar.UpdateProgress();
        yield return new WaitUntil(() => allQuestionsAnswered); // switches automatically to last set with about and feedback question
        logger.LogQuestionnairePanelSwitch();
        yield return new WaitForSeconds(waitForSeconds); // wait for 1 minute until button gets enabled! so participants cannot accidentaly continue

        // end and data upload
        yield return new WaitUntil(() => gotoNextPanel);
        // log finish panel switch and get event records for uploading
        logger.LogFinishPanelSwitch();
        experimentData.eventRecords = logger.GetEventRecords();

        Color opaque = Color.red;
        resultsUploadedText.color = opaque;
        resultsUploadedText.text = "Uploading in progress! Do NOT quit!";
        // send all the data to the server AND also store it locally (just in case)
        File.WriteAllText(Application.persistentDataPath + "/ParticipantData/" + ParticipantID + ".txt", JsonUtility.ToJson(experimentData, true));
        uploader.Send(experimentData);

        // after optional feedback switch to finish panel
        progressBar.UpdateProgress();
        SwitchPanel(finishPanel);

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
        resultsUploadedText.text = "Experiment results uploaded successfully! You can exit the application now! Thank you!";
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
