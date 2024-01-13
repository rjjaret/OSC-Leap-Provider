using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Leap; 
using Leap.Unity;


using UnityEditor;
using static LeapFeatureEnum;
using Unity.VisualScripting;

using TMPro;
using UnityEditor.Hardware;



/// <summary>
/// Simple OSC test communication script
/// </summary>
//[AddComponentMenu("Scripts/OSCTestSender")]
public class OSCSenderLeap : MonoBehaviour
{
    private LeapProvider _leapProvider;

    private Osc _oscHandler;
    private System.DateTime _nextSendTime;

    private TextMeshProUGUI _leftUIMessageHolder;
    private TextMeshProUGUI _rightUIMessageHolder;

    private string _leftHandUIText;
    private string _rightHandUIText;

     // values maintained for calculated fields
     private SortedList<LeapFeatureEnum, float> _previousValuesList;
    
    private int _featureEnumLength = Enum.GetNames(typeof(LeapFeatureEnum)).Length;
    private int _allPossibleFeaturesCount;

    // Serialized Fields
    [SerializeField]
    private string _remoteIP = "127.0.0.1";
    [SerializeField]
    private int _sendToPort = 6448;
    [SerializeField]
    private int _sendIntervalInMilli = 10;

    [SerializeField]
    private LeapFeatureEnum[] _featuresToSend = {RightPalmPositionX, RightPalmPositionZ, RightPalmNormalX, RightPalmNormalY, RightPalmNormalZ, RightPalmVelocityX,
    RightPalmVelocityY,
    RightPalmVelocityZ};

    [SerializeField]
    private LeapFeatureEnum[] _featuresToSendFirstOrderDiff = {RightPalmVelocityX,
    RightPalmVelocityY,
    RightPalmVelocityZ, LeftPalmVelocityY};

    [SerializeField]
    private SortedList<LeapFeatureEnum, float> _featuresToCheckForThreshold = new();

    [SerializeField]
   private SortedList<LeapFeatureEnum, float> _featuresToCheckFirstOrderForThreshold = new();

    [SerializeField]
   private bool _includeBones = false;

   //private int _currentNumbIntervalsBetweenHits = 0;
   // public properties
     public string RemoteIP { get => _remoteIP; set => _remoteIP = value; }
  
    public int SendToPort { get => _sendToPort; set => _sendToPort = value; }
    
    public int SendIntervalInMilli { get => _sendIntervalInMilli; set => _sendIntervalInMilli = value; }
   
    public LeapFeatureEnum[] FeatureList { get => _featuresToSend; set => _featuresToSend = value; }

    ~OSCSenderLeap()
    {
        if (_oscHandler != null)
        {            
            _oscHandler.Cancel();
        }

        // speed up finalization
        _oscHandler = null;
        System.GC.Collect();
    }

 
    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// </summary>
    void Awake()
    {
       
    }

    void OnDisable()
    {

        // close OSC UDP socket
        Debug.Log("closing OSC UDP socket in OnDisable");
        _oscHandler.Cancel();
        _oscHandler = null;
    }


    /// <summary>
    /// Start is called just before any of the Update methods is called the first time.
    /// </summary>
    void Start()
    {
        UDPPacketIO udp = GetComponent<UDPPacketIO>();
        udp.init(RemoteIP, SendToPort);
        udp.Open();
        _oscHandler = GetComponent<Osc>();
        _oscHandler.init(udp);

        _nextSendTime = DateTime.Now;
        _leapProvider = UnityEngine.Object.FindAnyObjectByType<LeapProvider>();

        _allPossibleFeaturesCount = _featureEnumLength;

        InitFirstOrderStates();
        InitFeatureThresholds();
        SendInputNames();

        _rightUIMessageHolder = UnityEngine.GameObject.FindGameObjectWithTag("RightUIMessageHolder").ConvertTo<TextMeshProUGUI>();
        _leftUIMessageHolder = UnityEngine.GameObject.FindGameObjectWithTag("LeftUIMessageHolder").ConvertTo<TextMeshProUGUI>();
    }



    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        DateTime now = System.DateTime.Now;

        if (DateTime.Compare(now, _nextSendTime) > 0)
        {
            // if (CheckMinimumNumberIntervalsMetAndIncrement())
            //  {   
                this.sendOsc();
            //  }
            
            _nextSendTime = now.AddMilliseconds(_sendIntervalInMilli);

        }
    }

    private void sendOsc()
    {
        OscMessage msg = new OscMessage();
        //string[] weekDays = ["hello", "goodbye"]

        if(BuildOscFeaturesValueMessage(ref msg))
        {
            Debug.Log("Preparing message . . ");
            _oscHandler.Send(msg);
            this.BuildUIText(msg);
            //Debug.Log("OSC message sent: [ " + msg.Values.ToString() + "]");
        }
    }

    private void BuildUIText(OscMessage msg)
    {
        _rightHandUIText = "";
        _leftHandUIText = "";

        // Build UI messages
        Type lfeType = typeof(LeapFeatureEnum);
        int i = 0;
        foreach (LeapFeatureEnum feature in _featuresToSend)
        {
            int featureID = (int)feature;
            float featureValue = (float)msg.Values[i];
            string featureName = Enum.GetName(lfeType, featureID);

            if (featureName.Truncate(3, "") == "Lef")
                _leftHandUIText += featureName + ":      " + featureValue + Environment.NewLine; // + Environment.NewLine;
            else
                _rightHandUIText += featureName + ":      " + featureValue + Environment.NewLine; // + Environment.NewLine;
            i++;
        }

        // add first orders to UI messages
        i = 0;
        foreach (LeapFeatureEnum feature in _featuresToSendFirstOrderDiff)
        {
            int featureID = (int)feature;
            int firstOrdinalfeatureID = i + _featuresToSend.Length;
            float featureValue = (float)msg.Values[firstOrdinalfeatureID];
            string featureName = Enum.GetName(lfeType, featureID) + "-1stOrd";

            if (featureName.Truncate(3, "") == "Lef")
                _leftHandUIText += featureName + ":      " + featureValue + Environment.NewLine; // + Environment.NewLine;
            else
                _rightHandUIText += featureName + ":      " + featureValue + Environment.NewLine; // + Environment.NewLine;
            i++;
        }

        _leftUIMessageHolder.text = _leftHandUIText;
        _rightUIMessageHolder.text = _rightHandUIText;

        // Debug.Log("Left Side:" + Environment.NewLine + _leftHandUIText);
        // Debug.Log("Right Side:" + Environment.NewLine + _rightHandUIText);
    }

    private bool BuildOscFeaturesValueMessage(ref OscMessage msg)
    {
        bool rtn = false;
        //Debug.Log("BuildOscMessage: Start");

        if(_leapProvider.CurrentFrame.Hands.Count > 0 )
        {
                msg.Address = "/wek/inputs";
                float[] features = new float[_allPossibleFeaturesCount];

                foreach (Hand hand in _leapProvider.CurrentFrame.Hands)
                {          
                    this.GetArmFeatures(hand, ref features);
                    this.GetHandFeatures(hand, ref features);
                    this.GetFingerFeatures(hand, ref features);
                    if (_includeBones)
                        this.GetBoneFeatures(hand, ref features);
                }

                this.AddFirstOrderDiffsToFeatures(ref features, ref msg); // has to be called after all other features added                

                if (CheckThresholds(features))
                {
                    for (int i = 0; i < _featuresToSend.Length; i++)
                    {
                        int featureID = (int)_featuresToSend[i];
                        float featureValue = (float)features[featureID];
                        msg.Values.Add(featureValue);
                    }

                    // add first order diffs
                    AddFirstOrderDiffsToMesssage(features, ref msg); // has to be called after all other values added.                    

                    rtn = true;
                }
                //Debug.Log(uiMessage);
        }

        //Debug.Log("BuildOscMessage: End");
        return rtn;
    }
 
    private bool CheckThresholds(float[] allFeatures)
    {
        bool rtn = false;

        foreach (System.Collections.Generic.KeyValuePair<LeapFeatureEnum, float> featureToCheck in  _featuresToCheckForThreshold)
        {
            int featureID = (int)featureToCheck.Key;
            float featureValue = (float)allFeatures[featureID];
            float threshold = featureToCheck.Value;

            if (threshold >= 0 && featureValue > threshold)
            {
                rtn = true;
                break;
            }
            else if (threshold < 0 && featureValue < threshold)
            {
                rtn = true;
                break;
            }
        }
        
        if (rtn == false)
        {
            foreach (System.Collections.Generic.KeyValuePair<LeapFeatureEnum, float> featureToCheck in  _featuresToCheckFirstOrderForThreshold)
            {
                int foFeatureID = GetFirstOrderFeatureID (featureToCheck.Key);
                float featureValue = (float)allFeatures[foFeatureID];
                float threshold = featureToCheck.Value;

                if (threshold >= 0 && featureValue > threshold)
                {
                    rtn = true;
                    break;
                }
                else if (threshold < 0 && featureValue < threshold)
                {
                    rtn = true;
                    break;
                }
            }
        }

        return rtn;
    }
    
    private int GetFirstOrderFeatureID(LeapFeatureEnum feature)
    {
        int i = 0;
        int foFeatureID = 0;
        foreach (LeapFeatureEnum featureToSendFirstOrder in _featuresToSendFirstOrderDiff)
        {
            if (featureToSendFirstOrder == feature)
            {
                foFeatureID = _featureEnumLength + i;
                break;

            }
            i++;
        }
        return foFeatureID;
    }
    

    private void InitFirstOrderStates()
    {
        if (_featuresToSendFirstOrderDiff.Length > 0)
        {
            _previousValuesList = new SortedList<LeapFeatureEnum, float>(_featuresToSendFirstOrderDiff.Length);
   
            foreach (LeapFeatureEnum feature in _featuresToSendFirstOrderDiff)
            {
                int featureID = (int) feature;
                _previousValuesList.Add(feature, 0.00f);
                _allPossibleFeaturesCount ++; // increase the total number of available features. used to dim 
            }
        }
    }
    private void InitFeatureThresholds()    {
        //TODO - try approach serialize sorgted list so it shows up in Unity UI
        _featuresToCheckForThreshold = new SortedList<LeapFeatureEnum, float>();
        _featuresToCheckFirstOrderForThreshold = new SortedList<LeapFeatureEnum, float>();

        _featuresToCheckForThreshold.Add(LeapFeatureEnum.LeftPalmVelocityX, -1f);
        _featuresToCheckForThreshold.Add(LeapFeatureEnum.RightPalmVelocityX, -1f);

        _featuresToCheckFirstOrderForThreshold.Add(LeapFeatureEnum.LeftPalmVelocityX, -1f);
        _featuresToCheckFirstOrderForThreshold.Add(LeapFeatureEnum.RightPalmVelocityX, -1f);

        _featuresToCheckFirstOrderForThreshold.Add(LeapFeatureEnum.LeftPalmVelocityY, -1f);
        _featuresToCheckFirstOrderForThreshold.Add(LeapFeatureEnum.RightPalmVelocityY, -1f);
        
        _featuresToCheckFirstOrderForThreshold.Add(LeapFeatureEnum.LeftPalmVelocityZ, -1f);
        _featuresToCheckFirstOrderForThreshold.Add(LeapFeatureEnum.RightPalmVelocityZ, -1f);
    }

    private void AddFirstOrderDiffsToFeatures(ref float[] features, ref OscMessage msg)
    {
        if (_featuresToSendFirstOrderDiff.Length > 0)
        {
            foreach (LeapFeatureEnum feature in _featuresToSendFirstOrderDiff)
            {
                int featureID = (int)feature;

                float lastValue = (float) _previousValuesList[feature];
                float currentValue = (float)features[featureID];
                float foValue = 0f;

                int firstOrderFeatureID = GetFirstOrderFeatureID(feature);

                if (Math.Abs(lastValue) + Math.Abs(currentValue) != 0f)
                {
                    foValue = CalculateFirstOrdinalPerInterval(currentValue, lastValue);                    
                }
        
                _previousValuesList[feature] = currentValue;
                features[firstOrderFeatureID] = foValue;
            }
        }
    }

      private void AddFirstOrderDiffsToMesssage(float[] features, ref OscMessage msg)
    {
        if (_featuresToSendFirstOrderDiff.Length > 0)
        {
            foreach (LeapFeatureEnum feature in _featuresToSendFirstOrderDiff)
            {
                int firstOrderFeatureID = GetFirstOrderFeatureID(feature);
                float value = features[firstOrderFeatureID];

                msg.Values.Add(value);
            }
        }
    }

    private float CalculateFirstOrdinalPerInterval(float currentValue, float lastValue)
    {
        float rtn = currentValue - lastValue;
        return rtn;
    }

// To send names to wekinator, open up Wekinator. On the 'Create new project' screen, makes sure Wekinator
// listening. Then start this app and this function will send the input names over to Wekinator in Start()
    private void SendInputNames()
    {
            Type enumType = typeof(LeapFeatureEnum);
            OscMessage msg = new OscMessage();
            msg.Address =  "/wekinator/control/setInputNames";

            foreach(LeapFeatureEnum  featureID in  _featuresToSend)
            {
                    string featureName = Enum.GetName(typeof(LeapFeatureEnum), featureID);
                    msg.Values.Add(featureName);
            }

            foreach(LeapFeatureEnum  featureID in  _featuresToSendFirstOrderDiff)
            {
                    string featureName = Enum.GetName(typeof(LeapFeatureEnum), featureID) + "-1stOrd";
                    msg.Values.Add(featureName);
            }

            _oscHandler.Send(msg);
            Debug.Log("SendFeatureNames: End");
    }


    private void GetHandFeatures(Hand hand, ref float[] features)
    {
        if (hand.IsLeft)
        {
            features[(int)LeftHandDirMagnitude] = hand.Direction.magnitude;
            features[(int)LeftHandSqrMagnitude] = hand.Direction.sqrMagnitude;
            features[(int)LeftHandDirectionX] = hand.Direction.x;
            features[(int)LeftHandDirectionY] = hand.Direction.y;
            features[(int)LeftHandDirectionZ] = hand.Direction.z;
            features[(int)LeftWristX] = hand.WristPosition.x;
            features[(int)LeftWristY] = hand.WristPosition.y;
            features[(int)LeftWristZ] = hand.WristPosition.z;
            features[(int)LeftPalmPositionX] = hand.PalmPosition.x;
            features[(int)LeftPalmPositionX] = hand.PalmPosition.y;
            features[(int)LeftPalmPositionZ] = hand.PalmPosition.z;
            features[(int)LeftPalmVelocityX] = hand.PalmVelocity.x;
            features[(int)LeftPalmVelocityY] = hand.PalmVelocity.y;
            features[(int)LeftPalmVelocityZ] = hand.PalmVelocity.z;
            features[(int)LeftPalmVelocityMagnitude] = hand.PalmVelocity.magnitude;
            features[(int)LeftPalmNormalX] = hand.PalmNormal.x;
            features[(int)LeftPalmNormalY] = hand.PalmNormal.y;
            features[(int)LeftPalmNormalZ] = hand.PalmNormal.z;
            features[(int)LeftPalmNormalMagnitude] = hand.PalmNormal.magnitude;



            
        } else if(hand.IsRight)
        {
            features[(int)RightHandDirMagnitude] = hand.Direction.magnitude;
            features[(int)RightHandSqrMagnitude] = hand.Direction.sqrMagnitude;
            features[(int)RightHandDirectionX] = hand.Direction.x;
            features[(int)RightHandDirectionY] = hand.Direction.y;
            features[(int)RightHandDirectionZ] = hand.Direction.z;
            features[(int)RightWristX] = hand.WristPosition.x;
            features[(int)RightWristY] = hand.WristPosition.y;
            features[(int)RightWristZ] = hand.WristPosition.z;
            features[(int)RightPalmPositionX] = hand.PalmPosition.x;
            features[(int)RightPalmPositionY] = hand.PalmPosition.y;
            features[(int)RightPalmPositionZ] = hand.PalmPosition.z;
            features[(int)RightPalmVelocityX] = hand.PalmVelocity.x;
            features[(int)RightPalmVelocityY] = hand.PalmVelocity.y;
            features[(int)RightPalmVelocityZ] = hand.PalmVelocity.z;
            features[(int)RightPalmVelocityMagnitude] = hand.PalmVelocity.magnitude;
            features[(int)RightPalmNormalX] = hand.PalmNormal.x;
            features[(int)RightPalmNormalY] = hand.PalmNormal.y;
            features[(int)RightPalmNormalZ] = hand.PalmNormal.z;
            features[(int)RightPalmNormalMagnitude] = hand.PalmNormal.magnitude;
        }

        this.GetArmFeatures(hand, ref features);
        this.GetFingerFeatures(hand, ref features);
    }
    
 private void GetFingerFeatures(Hand hand, ref float[]features)
    {
        Finger[] fingers = new Finger[5];
        //Use _hand to Explicitly get the specified finger and subsequent bone from it
        if (hand.IsLeft)
        {
        fingers[0]  = hand.GetThumb();
        fingers[1] = hand.GetIndex();
        fingers[2] = hand.GetMiddle();
        fingers[3] = hand.GetRing();
        fingers[4] = hand.GetPinky();

        features[(int)LeftThumbTipX] = fingers[0].TipPosition.x;
        features[(int)LeftThumbTipY] = fingers[0].TipPosition.y;
        features[(int)LeftThumbTipZ] = fingers[0].TipPosition.z;
        features[(int)LeftThumbLength] = fingers[0].Length;

        features[(int)LeftIndexTipX] = fingers[1].TipPosition.x;
        features[(int)LeftIndexTipY] = fingers[1].TipPosition.y;
        features[(int)LeftIndexTipZ] = fingers[1].TipPosition.z;
        features[(int)LeftIndexLength] = fingers[1].Length;

        features[(int)LeftMiddleTipX] = fingers[2] .TipPosition.x;
        features[(int)LeftMiddleTipY] = fingers[2] .TipPosition.y;
        features[(int)LeftMiddleTipZ] = fingers[2] .TipPosition.z;
        features[(int)LeftMiddleLength] = fingers[2] .Length;

        features[(int)LeftRingTipX] = fingers[3].TipPosition.x;
        features[(int)LeftRingTipY] = fingers[3].TipPosition.y;
        features[(int)LeftRingTipZ] = fingers[3].TipPosition.z;
        features[(int)LeftRingLength] = fingers[3].Length;

        features[(int)LeftPinkyTipX] = fingers[4].TipPosition.x;
        features[(int)LeftPinkyTipY] = fingers[4].TipPosition.y;
        features[(int)LeftPinkyTipZ] = fingers[4].TipPosition.z;
        features[(int)LeftPinkyLength] = fingers[4].Length;
    }
    else if (hand.IsRight)
    {
        fingers[0]  = hand.GetThumb();
        fingers[1] = hand.GetIndex();
        fingers[2] = hand.GetMiddle();
        fingers[3] = hand.GetRing();
        fingers[4] = hand.GetPinky();

        features[(int)RightThumbTipX] = fingers[0].TipPosition.x;
        features[(int)RightThumbTipY] = fingers[0].TipPosition.y;
        features[(int)RightThumbTipZ] = fingers[0].TipPosition.z;
        features[(int)RightThumbLength] = fingers[0].Length;

        features[(int)RightIndexTipX] = fingers[1].TipPosition.x;
        features[(int)RightIndexTipY] = fingers[1].TipPosition.y;
        features[(int)RightIndexTipZ] = fingers[1].TipPosition.z;
        features[(int)RightIndexLength] = fingers[1].Length;

        features[(int)RightMiddleTipX] = fingers[2] .TipPosition.x;
        features[(int)RightMiddleTipY] = fingers[2] .TipPosition.y;
        features[(int)RightMiddleTipZ] = fingers[2] .TipPosition.z;
        features[(int)RightMiddleLength] = fingers[2] .Length;

        features[(int)RightRingTipX] = fingers[3].TipPosition.x;
        features[(int)RightRingTipY] = fingers[3].TipPosition.y;
        features[(int)RightRingTipZ] = fingers[3].TipPosition.z;
        features[(int)RightRingLength] = fingers[3].Length;

        features[(int)RightPinkyTipX] = fingers[4].TipPosition.x;
        features[(int)RightPinkyTipY] = fingers[4].TipPosition.y;
        features[(int)RightPinkyTipZ] = fingers[4].TipPosition.z;
        features[(int)RightPinkyLength] = fingers[4].Length;
    }

    }

    private void GetBoneFeatures(Hand hand, ref float[]features)
    {

        Bone[] bones = new Bone[4];
        Finger finger;

        if (hand.IsLeft)
        {
            finger = hand.GetThumb();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);
            
            features[(int)LeftThumbMetacarpalX] = bones[0].Center.x;
            features[(int)LeftThumbMetacarpalY] = bones[0].Center.y;
            features[(int)LeftThumbMetacarpalZ] = bones[0].Center.z;
            features[(int)LeftThumbProximalX] = bones[1].Center.x;
            features[(int)LeftThumbProximalY] = bones[1].Center.y;
            features[(int)LeftThumbProximalZ] = bones[1].Center.z;
            features[(int)LeftThumbIntermediateX] = bones[2].Center.x;
            features[(int)LeftThumbIntermediateY] = bones[2].Center.y;
            features[(int)LeftThumbIntermediateZ] = bones[2].Center.z;
            features[(int)LeftThumbDistalX] = bones[3].Center.x;
            features[(int)LeftThumbDistalX] = bones[3].Center.y;
            features[(int)LeftThumbDistalX] = bones[3].Center.z;

            finger = hand.GetIndex();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);
            
            features[(int)LeftIndexMetacarpalX] = bones[0].Center.x;
            features[(int)LeftIndexMetacarpalY] = bones[0].Center.y;
            features[(int)LeftIndexMetacarpalZ] = bones[0].Center.z;
            features[(int)LeftIndexProximalX] = bones[1].Center.x;
            features[(int)LeftIndexProximalY] = bones[1].Center.y;
            features[(int)LeftIndexProximalZ] = bones[1].Center.z;
            features[(int)LeftIndexIntermediateX] = bones[2].Center.x;
            features[(int)LeftIndexIntermediateY] = bones[2].Center.y;
            features[(int)LeftIndexIntermediateZ] = bones[2].Center.z;
            features[(int)LeftIndexDistalX] = bones[3].Center.x;
            features[(int)LeftIndexDistalX] = bones[3].Center.y;
            features[(int)LeftIndexDistalX] = bones[3].Center.z;      

            finger=hand.GetMiddle();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);

            features[(int)LeftMiddleMetacarpalX] = bones[0].Center.x;
            features[(int)LeftMiddleMetacarpalY] = bones[0].Center.y;
            features[(int)LeftMiddleMetacarpalZ] = bones[0].Center.z;
            features[(int)LeftMiddleProximalX] = bones[1].Center.x;
            features[(int)LeftMiddleProximalY] = bones[1].Center.y;
            features[(int)LeftMiddleProximalZ] = bones[1].Center.z;
            features[(int)LeftMiddleIntermediateX] = bones[2].Center.x;
            features[(int)LeftMiddleIntermediateY] = bones[2].Center.y;
            features[(int)LeftMiddleIntermediateZ] = bones[2].Center.z;
            features[(int)LeftMiddleDistalX] = bones[3].Center.x;
            features[(int)LeftMiddleDistalX] = bones[3].Center.y;
            features[(int)LeftMiddleDistalX] = bones[3].Center.z;                      

            finger = hand.GetRing();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);

            features[(int)LeftRingMetacarpalX] = bones[0].Center.x;
            features[(int)LeftRingMetacarpalY] = bones[0].Center.y;
            features[(int)LeftRingMetacarpalZ] = bones[0].Center.z;
            features[(int)LeftRingProximalX] = bones[1].Center.x;
            features[(int)LeftRingProximalY] = bones[1].Center.y;
            features[(int)LeftRingProximalZ] = bones[1].Center.z;
            features[(int)LeftRingIntermediateX] = bones[2].Center.x;
            features[(int)LeftRingIntermediateY] = bones[2].Center.y;
            features[(int)LeftRingIntermediateZ] = bones[2].Center.z;
            features[(int)LeftRingDistalX] = bones[3].Center.x;
            features[(int)LeftRingDistalX] = bones[3].Center.y;
            features[(int)LeftRingDistalX] = bones[3].Center.z;                      

            finger = hand.GetPinky();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);

            features[(int)LeftPinkyMetacarpalX] = bones[0].Center.x;
            features[(int)LeftPinkyMetacarpalY] = bones[0].Center.y;
            features[(int)LeftPinkyMetacarpalZ] = bones[0].Center.z;
            features[(int)LeftPinkyProximalX] = bones[1].Center.x;
            features[(int)LeftPinkyProximalY] = bones[1].Center.y;
            features[(int)LeftPinkyProximalZ] = bones[1].Center.z;
            features[(int)LeftPinkyIntermediateX] = bones[2].Center.x;
            features[(int)LeftPinkyIntermediateY] = bones[2].Center.y;
            features[(int)LeftPinkyIntermediateZ] = bones[2].Center.z;
            features[(int)LeftPinkyDistalX] = bones[3].Center.x;
            features[(int)LeftPinkyDistalX] = bones[3].Center.y;
            features[(int)LeftPinkyDistalX] = bones[3].Center.z;            
        }
        
        else if (hand.IsRight)
        {            finger = hand.GetThumb();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);
            
            features[(int)RightThumbMetacarpalX] = bones[0].Center.x;
            features[(int)RightThumbMetacarpalY] = bones[0].Center.y;
            features[(int)RightThumbMetacarpalZ] = bones[0].Center.z;
            features[(int)RightThumbProximalX] = bones[1].Center.x;
            features[(int)RightThumbProximalY] = bones[1].Center.y;
            features[(int)RightThumbProximalZ] = bones[1].Center.z;
            features[(int)RightThumbIntermediateX] = bones[2].Center.x;
            features[(int)RightThumbIntermediateY] = bones[2].Center.y;
            features[(int)RightThumbIntermediateZ] = bones[2].Center.z;
            features[(int)RightThumbDistalX] = bones[3].Center.x;
            features[(int)RightThumbDistalX] = bones[3].Center.y;
            features[(int)RightThumbDistalX] = bones[3].Center.z;

            finger = hand.GetIndex();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);
            
            features[(int)RightIndexMetacarpalX] = bones[0].Center.x;
            features[(int)RightIndexMetacarpalY] = bones[0].Center.y;
            features[(int)RightIndexMetacarpalZ] = bones[0].Center.z;
            features[(int)RightIndexProximalX] = bones[1].Center.x;
            features[(int)RightIndexProximalY] = bones[1].Center.y;
            features[(int)RightIndexProximalZ] = bones[1].Center.z;
            features[(int)RightIndexIntermediateX] = bones[2].Center.x;
            features[(int)RightIndexIntermediateY] = bones[2].Center.y;
            features[(int)RightIndexIntermediateZ] = bones[2].Center.z;
            features[(int)RightIndexDistalX] = bones[3].Center.x;
            features[(int)RightIndexDistalX] = bones[3].Center.y;
            features[(int)RightIndexDistalX] = bones[3].Center.z;      

            finger=hand.GetMiddle();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);

            features[(int)RightMiddleMetacarpalX] = bones[0].Center.x;
            features[(int)RightMiddleMetacarpalY] = bones[0].Center.y;
            features[(int)RightMiddleMetacarpalZ] = bones[0].Center.z;
            features[(int)RightMiddleProximalX] = bones[1].Center.x;
            features[(int)RightMiddleProximalY] = bones[1].Center.y;
            features[(int)RightMiddleProximalZ] = bones[1].Center.z;
            features[(int)RightMiddleIntermediateX] = bones[2].Center.x;
            features[(int)RightMiddleIntermediateY] = bones[2].Center.y;
            features[(int)RightMiddleIntermediateZ] = bones[2].Center.z;
            features[(int)RightMiddleDistalX] = bones[3].Center.x;
            features[(int)RightMiddleDistalX] = bones[3].Center.y;
            features[(int)RightMiddleDistalX] = bones[3].Center.z;                      

            finger = hand.GetRing();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);

            features[(int)RightRingMetacarpalX] = bones[0].Center.x;
            features[(int)RightRingMetacarpalY] = bones[0].Center.y;
            features[(int)RightRingMetacarpalZ] = bones[0].Center.z;
            features[(int)RightRingProximalX] = bones[1].Center.x;
            features[(int)RightRingProximalY] = bones[1].Center.y;
            features[(int)RightRingProximalZ] = bones[1].Center.z;
            features[(int)RightRingIntermediateX] = bones[2].Center.x;
            features[(int)RightRingIntermediateY] = bones[2].Center.y;
            features[(int)RightRingIntermediateZ] = bones[2].Center.z;
            features[(int)RightRingDistalX] = bones[3].Center.x;
            features[(int)RightRingDistalX] = bones[3].Center.y;
            features[(int)RightRingDistalX] = bones[3].Center.z;                      

            finger = hand.GetPinky();
            bones.Initialize();
            bones[0] = finger.Bone(Bone.BoneType.TYPE_METACARPAL);
            bones[1]  = finger.Bone(Bone.BoneType.TYPE_PROXIMAL);
            bones[2]  = finger.Bone(Bone.BoneType.TYPE_DISTAL);
            bones[3]  = finger.Bone(Bone.BoneType.TYPE_INTERMEDIATE);

            features[(int)RightPinkyMetacarpalX] = bones[0].Center.x;
            features[(int)RightPinkyMetacarpalY] = bones[0].Center.y;
            features[(int)RightPinkyMetacarpalZ] = bones[0].Center.z;
            features[(int)RightPinkyProximalX] = bones[1].Center.x;
            features[(int)RightPinkyProximalY] = bones[1].Center.y;
            features[(int)RightPinkyProximalZ] = bones[1].Center.z;
            features[(int)RightPinkyIntermediateX] = bones[2].Center.x;
            features[(int)RightPinkyIntermediateY] = bones[2].Center.y;
            features[(int)RightPinkyIntermediateZ] = bones[2].Center.z;
            features[(int)RightPinkyDistalX] = bones[3].Center.x;
            features[(int)RightPinkyDistalX] = bones[3].Center.y;
            features[(int)RightPinkyDistalX] = bones[3].Center.z;     

        }
    }


    private void GetArmFeatures(Hand hand, ref float[] features)
    {
        Arm _arm = hand.Arm;

        Vector3 _elbowPosition = _arm.ElbowPosition;
        if (hand.IsLeft)
        {
            features[(int)LeftArmLength] = _arm.Length;
            features[(int)LeftArmWidth] = _arm.Width;
            features[(int)LeftArmCenterX] = _arm.Center.x;
            features[(int)LeftArmCenterY] = _arm.Center.y;
            features[(int)LeftArmCenterZ] = _arm.Center.z;
            features[(int)LeftArmElbowPositionX] = _arm.ElbowPosition.x;
            features[(int)LeftArmElbowPositionY] = _arm.ElbowPosition.y;
            features[(int)LeftArmElbowPositionZ] = _arm.ElbowPosition.z;
        }
        else if (hand.IsRight)
        {
            features[(int)RightArmLength] = _arm.Length;
            features[(int)RightArmWidth] = _arm.Width;
            features[(int)RightArmCenterX] = _arm.Center.x;
            features[(int)RightArmCenterY] = _arm.Center.y;
            features[(int)RightArmCenterZ] = _arm.Center.z;
            features[(int)RightArmElbowPositionX] = _arm.ElbowPosition.x;
            features[(int)RightArmElbowPositionY] = _arm.ElbowPosition.y;
            features[(int)RightArmElbowPositionZ] = _arm.ElbowPosition.z;
        }

    }
}
