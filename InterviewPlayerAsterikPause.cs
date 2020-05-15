using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Whilefun.FPEKit;

public class InterviewPlayer : MonoBehaviour
{



    private bool triggeredAudioSource = false;
    private AudioSource myAudioSource;
    private Transform playerTransform;

    public float distanceFromPlayer = 2;
    public float boxFadeTime = 1f;

    private string thisConversationName;
    public InterviewData thisInterview;

    private string transcriptionString;
    private string portraitName;
    
    private ConversationInfo thisConversationInfo;
    private ConversationInfo introductionConversationInfo;
    private ConversationInfo currentConversationInfo;

    private int currentChunkIndex = 0;

    private bool alreadyListenedToThis = false;

    private bool isPaused = false;

    //Variables for calculating typing-out. Since all calculations take place inside update, i needed to define these here.
    private bool _isTypingOut;
    private int _chunkIndex = 0;
    private bool _setNewChunk;
    private float _lerpPercent;
    private float _currentChunkDuration;
    private float _timer;
    private float _asteriskTimer; // a timer used to determine how long to pause the text
    private string _transcriptionText = "";
    private int _cutIndex;
    private int _recentAsteriskIndex = 0; //keep track of the most recent asterisk we have encountered while iterating over the text

    private bool _playingIntro;
    private float introTimer;
    private float countTime = 0;

    private float _endOfAsteriskWaitTime = .5f; //amount of time to pause typing at the end of each asterisk (feel free to change based on preference)
    private float _endOfChunkWaitTime = 1f; //amount of time to pause typing at the end of each chunk (feel free to change based on preference)

    // Start is called before the first frame update
    void Start()
    {
        myAudioSource = GetComponent<AudioSource>();
        playerTransform = GameObject.FindWithTag("Player").GetComponent<Transform>();
        Debug.Assert(TranscriptionDataParser.instance.TranscriptionDataDictionary != null);
        thisConversationName = thisInterview.name;
        thisConversationInfo = TranscriptionDataParser.instance.TranscriptionDataDictionary[thisConversationName];





        //in the code below, need to change ""thisConversationInfo" to "currentConversationInfo"

    }

    // Update is called once per frame
    void Update()
    {

        float dist = Vector3.Distance(playerTransform.position, transform.position);
        if ( dist <= distanceFromPlayer )
            // if the player is within the declared distance...
        {
            if (!myAudioSource.isPlaying && triggeredAudioSource == false)
                // ...and if the clip hasn't already started playing...
            {
                if(TranscriptionDataParser.instance.metIntervieweeTracker.ContainsKey(thisConversationInfo.chunks[0].Speaker))
                {
                    //if first time talking to person
                    //set currentConversationInfo to the introductionConversationInfo
                    currentConversationInfo = TranscriptionDataParser.instance.metIntervieweeTracker[thisConversationInfo.chunks[0].Speaker];
                    TranscriptionDataParser.instance.metIntervieweeTracker.Remove(
                        thisConversationInfo.chunks[0].Speaker);
                    _playingIntro = true;
                    Debug.Assert(currentConversationInfo != null);
                    introTimer = currentConversationInfo.interviewData.specificClip.length;

                }
                if (!_playingIntro)
                {
                    //if NOT the first time talking to person, set the current conversation to the one associated with
                    //this individual object
                    currentConversationInfo = thisConversationInfo;
                }

                myAudioSource.PlayOneShot(currentConversationInfo.interviewData.specificClip);
                triggeredAudioSource = true;
                if (!FPEInterviewPlayerMenu.instance.textBox.IsActive())
                {
                    FPEInterviewPlayerMenu.instance.ShowTextBox();

                    //Start typing out text.
                    _isTypingOut = true; //set a flag to tell us to start the typing-out algorithm!
                    _setNewChunk = true; //set a flag to tell us to update to a new chunk
                    _chunkIndex = -1; //reset the chunkIndex to -1. (it will get set to 0 later)
                }

            }

        }

        if (_playingIntro)
        {
            countTime += Time.deltaTime;

            if (countTime >= introTimer)
            {
                countTime = 0;
                myAudioSource.PlayOneShot(thisConversationInfo.interviewData.specificClip);
            }
        }

        //HANDLE TYPING OUT TEXT---------------------------------------------------------------------------------------------------------------
        if (_isTypingOut)
        {
          _asteriskTimer -= Time.deltaTime;

          //if we are delaying the typing after finding an asterik, then call "return". nothing after this line will execute,
          //meaning the text will not keep typing out until after the _asteriskTimer is up.
          if(_asteriskTimer > 0){
            return;
          }

            _timer += Time.deltaTime; //increase timer so we know how much time is elapsed

            //The contents of the If-Statement below handle setting a new chunk. This should not happen every frame, hence
            //why we check the _setNewChunk flag to see if it has been switched on for us to update to the next chunk
            if (_setNewChunk)
            {
                _setNewChunk = false; //now set that boolean back to false. ensure we don't call this bit of code every frame.

                //if we have run out of chunks to display, either play the next conversation info, or hide txtbox
                if (_chunkIndex >= currentConversationInfo.chunks.Length - 1)
                {
                        if(_playingIntro)
                        {
                            currentConversationInfo = thisConversationInfo;
                            _chunkIndex = -1;
                            _playingIntro = false;
                        }
                        else
                        {
                            FPEInterviewPlayerMenu.instance.HideTextBox();
                            Invoke("ResetTranscriptionText", boxFadeTime * 2);
                        }
                }
                //otherwise, do some setup for the next chunk
                if(_chunkIndex < currentConversationInfo.chunks.Length - 1)
                {
                    _chunkIndex++; //move to the next chunk in the conversation
                    _timer = 0; //reset the timer to 0. (timer is only used to keep track of elapsed time between each chunk, which is why it should be 0 when we first set a new chunk)
                    _cutIndex = 0; //reset cut index (used for substring calculations later)
                    _recentAsteriskIndex = 0; //reset the location of the most recent asterik we have found

                    HandlePortraitActivation(currentConversationInfo.chunks[_chunkIndex].Speaker); //set new portrait, because someone new is talking

                    _transcriptionText = currentConversationInfo.chunks[_chunkIndex].speakerText; //get a reference to the speakerText in the current chunk

                    //calculate the duration of the current chunk based on timestamps.
                    if (_chunkIndex < currentConversationInfo.chunks.Length - 1)
                    {
                        //chunk duration is now set to the time we have between this timestamp and the next time stamp
                        _currentChunkDuration = (currentConversationInfo.chunks[_chunkIndex + 1].speakerTimestamp -
                                        currentConversationInfo.chunks[_chunkIndex].speakerTimestamp);
                    }
                    else
                    {
                        //i assume the full time of the interview minus the current time stamp (so the remainder of the interview time)
                        _currentChunkDuration = currentConversationInfo.interviewData.specificClip.length - currentConversationInfo.chunks[_chunkIndex].speakerTimestamp;
                    }

                }
            }

            //Ok here's where we type out the text. This algorithm runs every frame.

            //First we need to calculate the amount of time to offset the typing, so that we can have typing pauses
            //while still staying synched to the audio.
            float timeOffset = _endOfChunkWaitTime;

            //HANDLING THE PAUSE IN TYPING AFTER EACH CHUNK (BETWEEN CHARACTER SWITCHES)
            if (_currentChunkDuration <= _endOfChunkWaitTime)
            {
                timeOffset = _currentChunkDuration - .001f;
            }

            //HANDLING THE PAUSE IN TYPING AFTER EACH ASTERIK (WHEN THE TEXT OVERFLOWS)
            //Calculate the number of asteriks in this chunk, and for each asterk, add an amount of _endOfAsterikWaitTime to the offset
            timeOffset += (_transcriptionText.Split('*').Length - 1) * _endOfAsteriskWaitTime;


            //calculate what percent "done" we are with this chunk in terms of timing.
            //So if chunk is 10 seconds long, and 5 seconds has elapsed since we started the chunk, we are 50% done.
            _lerpPercent = _timer / (_currentChunkDuration - timeOffset);

            //get the current index in the string we should be at, based on the percent "done" we are with this chunk
            //So if we are 50% "done" with this chunk, and the transcriptionText has 200 characters, then the first 100 characters should be typed out.
            int charIndex = (int)Mathf.Lerp(0, _transcriptionText.Length, _lerpPercent);

            //Get all the characters we should have typed out so far based on the charIndex.
            //So if charIndex is 100, then totalStringSoFar will be a string with the first 100 characters of the total string
            string totalStringSoFar = _transcriptionText.Substring(0, charIndex);

            //Here is where we need to handle starting the text back at the top when we've reached overflow
            //check to see if there are any * in the text at all
            if (totalStringSoFar.Contains("*") )
            {
                //_cutIndex is a variable I am using to track where the beginning of the paragraph should start
                //so here we set the cut index to the most recent occurance of a * in the string. So the
                //typing-out will start one character after the most recent * found.
                _cutIndex = totalStringSoFar.LastIndexOf('*') + 1;

                //If the index of the asterik is new, meaning we have come across a new asterik, then that means
                //reset the asterik timer so that there can be a pause in the typing before starting a new paragraph
                if(_recentAsteriskIndex != _cutIndex){
                  _asteriskTimer = _endOfAsteriskWaitTime;
                  _recentAsteriskIndex = _cutIndex; //update the recent asterik index so that we don't run this if statement again until we find a new asterik
                  return; //return will end the Update loop here, so none of the code after this will be executed (nothing will be typed out)
                }
            }

            //Get the length of how many characters we want displayed on the screen this frame.
            int charCount = charIndex - _cutIndex;

            //Display a substring from the most recent "*" to the last character we should be typing out, based on
            //the percent "finished" we are with this chunk.
            if (_isTypingOut)
            {
                FPEInterviewPlayerMenu.instance.textBoxText.text = totalStringSoFar.Substring(_cutIndex, charCount);
            }

            //the _lerpPercent value will be greater than 1 when the _timer is >= the duration of the current chunk.
            //When this happens, we need to set the _setNewChunk flag to true, so that the next frame of Update,
            //we call the if-statement that sets a new chunk
            if (_lerpPercent > 1 && _timer >= _currentChunkDuration)
            {
                _setNewChunk = true;
            }

        }
        //---------------------------------------------------------------------------------------------------------------------------------------


        //handle pausing
        if (FPEGameMenu.Instance.menuActive)
        {
            if (myAudioSource.isPlaying)
            {
                myAudioSource.Pause();
                isPaused = true;
            }
        }

        if (FPEGameMenu.Instance.menuActive == false && isPaused)
        {
            myAudioSource.UnPause();
        }

        

    }




    /*private void ShowTextBox()
    {
        FPEInteractionManagerScript.Instance.disableMovement();

        if (GameManager.instance.vignette != null)
            DOTween.To(() => GameManager.instance.vignette.intensity.value,
                x => GameManager.instance.vignette.intensity.value = x, .45f, boxFadeTime);
        GameManager.instance.textBox.gameObject.SetActive(true);
        GameManager.instance.textBox.DOFade(.82f, boxFadeTime);
        GameManager.instance.textBoxText.DOFade(1f, boxFadeTime);
        GameManager.instance.portraitBackground.DOFade(1f, boxFadeTime);
        GameManager.instance.portraitBorder.DOFade(1f, boxFadeTime);
        GameManager.instance.portraitSprite.DOFade(1f, boxFadeTime);
        GameManager.instance.nameBox.DOFade(1f, boxFadeTime);
        GameManager.instance.nameText.DOFade(1f, boxFadeTime);
        Debug.Log("I, " + gameObject.name + ", have finished all ShowTextBox functionality");
    }

    private void HideTextBox()
    {
        FPEInteractionManagerScript.Instance.enableMovement();

        if (GameManager.instance.vignette != null)
            DOTween.To(() => GameManager.instance.vignette.intensity.value,
                x => GameManager.instance.vignette.intensity.value = x, 0, boxFadeTime);
        GameManager.instance.portraitBackground.DOFade(0f, boxFadeTime);
        GameManager.instance.portraitBorder.DOFade(0f, boxFadeTime);
        GameManager.instance.portraitSprite.DOFade(0f, boxFadeTime);
        GameManager.instance.textBox.DOFade(0f, boxFadeTime).OnComplete(() => GameManager.instance.textBox.gameObject.SetActive(false));
        GameManager.instance.textBoxText.DOFade(0f, boxFadeTime);
        GameManager.instance.nameBox.DOFade(0f, boxFadeTime);
        GameManager.instance.nameText.DOFade(0f, boxFadeTime);
        GameManager.instance.nameBoxBorder.DOFade(0f, boxFadeTime);
        Invoke("RefreshTextBox", boxFadeTime);

    } */

    private void ResetTranscriptionText()
    {
        FPEInterviewPlayerMenu.instance.textBoxText.text = "";
        _transcriptionText = "";
        gameObject.SetActive(false);
    }

    private void HandlePortraitActivation(Chunk.speakerName thisSpeaker)
    {
        if (thisSpeaker != Chunk.speakerName.none)
        {
            foreach (Chunk.speakerName interviewee in PortraitInfo.instance.portraitDictionary.Keys)
            {
                PortraitInfo.instance.portraitDictionary[interviewee].gameObject.SetActive(false);
            }

            PortraitInfo.instance.portraitDictionary[thisSpeaker].gameObject.SetActive(true);

            switch (thisSpeaker)
            {
                case Chunk.speakerName.AD:
                    FPEInterviewPlayerMenu.instance.nameText.text = "Dr. Andrea Douglas";
                    myAudioSource.volume = .5f;
                    break;
                case Chunk.speakerName.EB:
                    FPEInterviewPlayerMenu.instance.nameText.text = "Elizabeth Ballou";
                    myAudioSource.volume = .5f;
                    break;
                case Chunk.speakerName.JH:
                    FPEInterviewPlayerMenu.instance.nameText.text = "Dr. Jeffrey Hantman";
                    myAudioSource.volume = .5f;
                    break;
                case Chunk.speakerName.JS:
                    FPEInterviewPlayerMenu.instance.nameText.text = "Dr. Jalane Schmidt";
                    myAudioSource.volume = .5f;
                    break;
                case Chunk.speakerName.PL:
                    FPEInterviewPlayerMenu.instance.nameText.text = "Dr. Phyllis Leffler";
                    myAudioSource.volume = .5f;
                    break;
                case Chunk.speakerName.CC:
                    FPEInterviewPlayerMenu.instance.nameText.text = "Caro Campos";
                    break;
            }
        }
    }
    
}
