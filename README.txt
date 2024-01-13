Ultraleap Leap Motion Controller 2 Feature Extractor for OSC 

This feature extractor was built using:
Mac OS Ventura 13.6.1
Unity v. 2922.3.15f1
C# version 12.0, .Net 8.0
VS Code version 1.85.1


Git Repository: https://github.com/rjjaret/OSC-Leap-Provider

This feature extractor retrieves values from Leap Motion Controller 2 and selectively sends them in an OSC message based on those specified by the user. The UI consists of animations of each hand, with left and right text panels displaying feature values as they are sent. The Leap controller must be set up in desktop mode - on the desktop, just in front of the subject, with the green status light pointing away. 


This app was built as part of the Machine Learning for Musicians class offered online by Goldsmiths University of London via Kadenze:
https://www.kadenze.com/courses/machine-learning-for-musicians-and-artists-v. It combines features from the FeatureExtractor with the Leap input originally coded in Processing but which is obsolete, and adds a few things.  


Configuration must be done in the Unity editor as there is no admin screen at this time. Most parameters can be edited conveniently within the Inspector pane in the Unity Editor by opening the OSC Sender Leap Scene and selecting the OSC Sender - Leap prefab within the project hierarchy. 

Configuration Parameter Descriptions:

Remote IP:
ip where OSC messages should be sent. Defaults to 127.0.0.1

Send To Port: 
port where OSC messages should be sent. Defaults to 6448.

Send Interval in Milli: 
The time interval at which messages will be sent, or if the feature threshold is non-zero, the time interval at which the threshold will be evaluated. 

Features to Send:
List of features to package in the OSC message.

Features to Send First Order Diff:
List of features for which their 1st-order differential is to be packaged in the OSC message.

Include Bones:
Check if you'll be including any data on individual bones in the OSC message. If not, leave this unchecked to improve performance.

Due to their datatype, Unity Editor's serializer is unable to edit the following parameters in the Unity UI. They need to be edited in the InitFeatureThresholds() function in the file OSCSenderLeap.cs.

Features to Check for Threshold:  
List of features to evaluate against the given threshold to determine whether to send a message. 

Features to Check for First Order Threshold:  
List of features whose 1st-order diff values should be evaluated against the given threshold to determine whether to send a message. 


Potential Uses:
Any machine learning application that utilizes the current version of the Leap controller can make use of this tool as it provides access to many Leap parameters, and could be modified to retrieve others. I'm playing with two uses - an Air Drums midi-controller that will be used to train a model to send corresponding midi messages to a DAW. The other is an app to officiate a game of Rock, Paper, Scissors. 

Setup instructions for Unity and VS Code, including necessary libraries can be found here:
https://docs.ultraleap.com/xr-and-tabletop/xr/unity/getting-started/index.html

These instructions were very useful in setting up Unity and VSCode to work together (for a Mac environment):
https://www.youtube.com/watch?v=3GVGyooZ8jk

Compile:
You can compile as any Unity project and it will run as expected. It will function with whatever configuration parameters are assigned in the editor / code.





