using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

//You can set these variables in the scene because they are public 


/// <summary>
/// Simple OSC test communication script
/// </summary>
//[AddComponentMenu("Scripts/OSCReceiver")]
public class OSCReceiver : MonoBehaviour
{
    private string RemoteIP = "127.0.0.1";
    private int SendToPort = 6448;
    private int ListenerPort = 12000;
    private Transform controller;
    private Osc handler;
    private string Value1 = "0";
    private string Value2 = "0";


	void Start ()
	{
		//Initializes on start up to listen for messages
		//make sure this game object has both UDPPackIO and OSC script attached

		UDPPacketIO udp = (UDPPacketIO) GetComponent("UDPPacketIO");
		udp.init(RemoteIP, SendToPort, ListenerPort);
		handler = (Osc)GetComponent("Osc");
		handler.init(udp);

		//Tell Unity to call function Example1 when message /wek/outputs arrives
		handler.SetAddressHandler("/wek/outputs", Example1);

		UnityEngine.Debug.Log("OSC Running");
	
	}


	//Use the values from OSC to do stuff
	void Update () {
		var go = GameObject.Find("Capsule Hands");

		float x = 0f, y = 0f;

		if (float.TryParse(Value1, out x) && float.TryParse(Value2, out y))
			go.transform.Rotate(0, x, y);
		else
			go.transform.Rotate(0, 0, 0);
	}
	
	//This is called when /wek/outputs arrives, since this is what's specified in Start()
	void Example1(OscMessage oscMessage)
	{
		string msg = Osc.OscMessageToString(oscMessage);

        UnityEngine.Debug.Log("Called Example One > " + msg);
        UnityEngine.Debug.Log("Message Values > " + oscMessage.Values[0] + " " + oscMessage.Values[1]);
		
		object sig1 = oscMessage.Values[0].ToString();
		object sig2 = oscMessage.Values[1].ToString();
		
		Value1 = sig1.ToString();
		Value2 = sig2.ToString();

	}
} 