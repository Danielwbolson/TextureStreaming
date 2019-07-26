/*	VRPNClient.cpp
	VRPN Client Plugin for Unity
	Author: Evan Suma Rosenberg, Ph.D.
	Email: suma@umn.edu
	Copyright (c) 2019, University of Minnesota
*/

#if defined(_WIN32)
	#define EXPORT __declspec(dllexport)
#else
	#define EXPORT
#endif

#include "vrpn_Connection.h"
#include "vrpn_Tracker.h" 
#include "vrpn_Button.h" 
#include "vrpn_Analog.h" 

#include <string>
#include <vector>
#include <thread>
#include <mutex>
#include <chrono>
using namespace std;

#define MAIN_LOOP_THREAD_TIMEOUT 1

struct TrackerData
{
	double position[3];
	double rotation[4];
	double positionSum[3];
	double rotationSum[4];
	int numReports;
	mutex mtx;

	TrackerData()
	{
		position[0] = 0.0;
		position[1] = 0.0;
		position[2] = 0.0;
		rotation[0] = 0.0;
		rotation[1] = 0.0;
		rotation[2] = 0.0;
		rotation[3] = 1.0;
		positionSum[0] = 0.0;
		positionSum[1] = 0.0;
		positionSum[2] = 0.0;
		rotationSum[0] = 0.0;
		rotationSum[1] = 0.0;
		rotationSum[2] = 0.0;
		rotationSum[3] = 0.0;
		numReports = 0;
	}
};

struct ButtonData
{
	int state;

	ButtonData()
	{
		state = 0;
	}
};

struct AnalogData
{
	double state;

	AnalogData()
	{
		state = 0.0;
	}
};

struct VRPNTracker
{
	vrpn_Tracker_Remote* tracker;
	string serverName;
	vector<TrackerData*> data;
	unsigned int numInitializedTrackers;

	VRPNTracker(const char* name)
	{
		serverName = name;
		tracker = new vrpn_Tracker_Remote(name);
		numInitializedTrackers = 0;
	}
};

struct VRPNButton
{
	vrpn_Button_Remote* button;
	string serverName;
	vector<ButtonData*> data;
	unsigned int numInitializedButtons;

	VRPNButton(const char* name)
	{
		serverName = name;
		button = new vrpn_Button_Remote(name);
		numInitializedButtons = 0;
	}
};

struct VRPNAnalog
{
	vrpn_Analog_Remote* analog;
	string serverName;
	vector<AnalogData*> data;
	unsigned int numInitializedChannels;

	VRPNAnalog(const char* name)
	{
		serverName = name;
		analog = new vrpn_Analog_Remote(name);
		numInitializedChannels = 0;
	}
};

vector<VRPNTracker*> trackers;
vector<VRPNButton*> buttons;
vector<VRPNAnalog*> analogs;

thread* trackerUpdateThread = nullptr;
bool terminateTrackerUpdateThread = false;

void VRPN_CALLBACK trackerCallback(void *userdata, vrpn_TRACKERCB t)
{   
	VRPNTracker* tracker = static_cast<VRPNTracker*>(userdata);
	
	int sensorNumber = t.sensor;

	if(sensorNumber >= 0 && sensorNumber < (int)tracker->data.size() && tracker->data[sensorNumber] != nullptr)
	{
		TrackerData* trackerData = tracker->data[sensorNumber];

		if(trackerUpdateThread)
			trackerData->mtx.lock();

		trackerData->position[0] = t.pos[0];
		trackerData->position[1] = t.pos[1];
		trackerData->position[2] = t.pos[2];


		trackerData->rotation[0] = t.quat[0];
		trackerData->rotation[1] = t.quat[1];
		trackerData->rotation[2] = t.quat[2];
		trackerData->rotation[3] = t.quat[3];	

		trackerData->positionSum[0] += t.pos[0];
		trackerData->positionSum[1] += t.pos[1];
		trackerData->positionSum[2] += t.pos[2];

		trackerData->rotationSum[0] += t.quat[0];
		trackerData->rotationSum[1] += t.quat[1];
		trackerData->rotationSum[2] += t.quat[2];
		trackerData->rotationSum[3] += t.quat[3];

		trackerData->numReports++;

		if(trackerUpdateThread)
			trackerData->mtx.unlock();
	}
	
}

void VRPN_CALLBACK buttonCallback(void *userdata, const vrpn_BUTTONCB b)
{   
	VRPNButton* button = static_cast<VRPNButton*>(userdata);
	
	int buttonNumber = b.button;

	if(buttonNumber >= 0 && buttonNumber < (int)button->data.size() && button->data[buttonNumber] != nullptr)
	{
		button->data[buttonNumber]->state = b.state;
	}
}

void VRPN_CALLBACK analogCallback(void *userdata, const vrpn_ANALOGCB a)
{   
	VRPNAnalog* analog = static_cast<VRPNAnalog*>(userdata);
	
	for(int i=0; i < a.num_channel && i < (int)analog->data.size(); i++)
	{
		if(analog->data[i] != nullptr)
			analog->data[i]->state = a.channel[i];
	}
}

void trackerUpdateLoop()
{
	while(!terminateTrackerUpdateThread)
	{
		for(unsigned int i=0; i<trackers.size(); i++)
		{
			trackers[i]->tracker->mainloop();
		}

		this_thread::sleep_for(std::chrono::milliseconds(MAIN_LOOP_THREAD_TIMEOUT));
	}
}
 
extern "C"
{

	EXPORT void UpdateTrackersMultiThreaded()
	{
		for(unsigned int i=0; i<trackers.size(); i++)
		{
			for(unsigned int j=0; j<trackers[i]->data.size(); j++)
			{
				if(trackers[i]->data[j] != nullptr)
				{
					TrackerData* trackerData = trackers[i]->data[j];

					trackerData->mtx.lock();

					trackerData->positionSum[0] = 0.0;
					trackerData->positionSum[1] = 0.0;
					trackerData->positionSum[2] = 0.0;
					trackerData->rotationSum[0] = 0.0;
					trackerData->rotationSum[1] = 0.0;
					trackerData->rotationSum[2] = 0.0;
					trackerData->rotationSum[3] = 0.0;
					trackerData->numReports = 0;

					trackerData->mtx.unlock();
				}
			}
		}

		if(trackerUpdateThread == nullptr)
		{
			terminateTrackerUpdateThread = false;
			trackerUpdateThread = new thread(trackerUpdateLoop);
		}
	}

	EXPORT void UpdateTrackers()
	{
		for(unsigned int i=0; i<trackers.size(); i++)
		{
			for(unsigned int j=0; j<trackers[i]->data.size(); j++)
			{
				if(trackers[i]->data[j] != nullptr)
				{
					TrackerData* trackerData = trackers[i]->data[j];

					trackerData->positionSum[0] = 0.0;
					trackerData->positionSum[1] = 0.0;
					trackerData->positionSum[2] = 0.0;
					trackerData->rotationSum[0] = 0.0;
					trackerData->rotationSum[1] = 0.0;
					trackerData->rotationSum[2] = 0.0;
					trackerData->rotationSum[3] = 0.0;
					trackerData->numReports = 0;
				}
			}
		}

		for(unsigned int i=0; i<trackers.size(); i++)
		{
			trackers[i]->tracker->mainloop();
		}
	}

	EXPORT void UpdateButtons()
	{
		for(unsigned int i=0; i<buttons.size(); i++)
		{
			buttons[i]->button->mainloop();
		}
	}

	EXPORT void UpdateAnalogs()
	{
		for(unsigned int i=0; i<analogs.size(); i++)
		{
			analogs[i]->analog->mainloop();
		}
	}

	EXPORT TrackerData* InitializeTracker(const char* serverName, int sensorNumber)
	{
		int trackerNumber=-1;
		for(unsigned int i=0; i<trackers.size() && trackerNumber==-1; i++)
		{
			if(trackers[i]->serverName==serverName)
				trackerNumber = (int)i;
		}

		if(trackerNumber==-1)
		{
			trackerNumber = (int)trackers.size();
			VRPNTracker* newTracker = new VRPNTracker(serverName);
			trackers.push_back(newTracker);
			newTracker->tracker->register_change_handler(newTracker, trackerCallback);	
		}

		while(sensorNumber >= (int)trackers[trackerNumber]->data.size())
		{
			trackers[trackerNumber]->data.push_back(nullptr);
		}

		if(trackers[trackerNumber]->data[sensorNumber] == nullptr)
		{
			trackers[trackerNumber]->data[sensorNumber] = new TrackerData();
			trackers[trackerNumber]->numInitializedTrackers++;
		}

		return trackers[trackerNumber]->data[sensorNumber];
	}

	
	EXPORT ButtonData* InitializeButton(const char* serverName, int buttonNumber)
	{
		int serverNumber=-1;
		for(unsigned int i=0; i<buttons.size() && serverNumber==-1; i++)
		{
			if(buttons[i]->serverName==serverName)
				serverNumber = (int)i;
		}

		if(serverNumber==-1)
		{
			serverNumber = (int)buttons.size();
			VRPNButton* newButton = new VRPNButton(serverName);
			buttons.push_back(newButton);
			newButton->button->register_change_handler(newButton, buttonCallback);	
		}

		while(buttonNumber >= (int)buttons[serverNumber]->data.size())
		{
			buttons[serverNumber]->data.push_back(nullptr);
		}

		if(buttons[serverNumber]->data[buttonNumber] == nullptr)
		{
			buttons[serverNumber]->data[buttonNumber] = new ButtonData();
			buttons[serverNumber]->numInitializedButtons++;
		}

		return buttons[serverNumber]->data[buttonNumber];
	}

	EXPORT AnalogData* InitializeAnalog(const char* serverName, int channelNumber)
	{
		int serverNumber=-1;
		for(unsigned int i=0; i<analogs.size() && serverNumber==-1; i++)
		{
			if(analogs[i]->serverName==serverName)
				serverNumber = (int)i;
		}

		if(serverNumber==-1)
		{
			serverNumber = (int)analogs.size();
			VRPNAnalog* newAnalog = new VRPNAnalog(serverName);
			analogs.push_back(newAnalog);
			newAnalog->analog->register_change_handler(newAnalog, analogCallback);	
		}

		while(channelNumber >= (int)analogs[serverNumber]->data.size())
		{
			analogs[serverNumber]->data.push_back(nullptr);
		}

		if(analogs[serverNumber]->data[channelNumber] == nullptr)
		{
			analogs[serverNumber]->data[channelNumber] = new AnalogData();
			analogs[serverNumber]->numInitializedChannels++;
		}

		return analogs[serverNumber]->data[channelNumber];
	}

	EXPORT bool isTrackerConnected(const char* serverName)
	{
		for(unsigned int i=0; i<trackers.size(); i++)
		{
			if(trackers[i]->serverName==serverName)
			{
				if(trackers[i]->tracker->connectionPtr()->connected())
					return true;
				else
					return false;
			}
		}

		return false;
	}

	EXPORT bool IsButtonConnected(const char* serverName)
	{
		for(unsigned int i=0; i<buttons.size(); i++)
		{
			if(buttons[i]->button->connectionPtr()->connected())
					return true;
				else
					return false;
		}

		return false;
	}

	EXPORT bool IsAnalogConnected(const char* serverName)
	{
		for(unsigned int i=0; i<analogs.size(); i++)
		{
			if(analogs[i]->analog->connectionPtr()->connected())
					return true;
				else
					return false;
		}

		return false;
	}

	EXPORT void RemoveTracker(const char* serverName, int sensorNumber)
	{
		if(trackerUpdateThread)
		{
			terminateTrackerUpdateThread = true;
			trackerUpdateThread->join();
			delete trackerUpdateThread;
			trackerUpdateThread = nullptr;
		}

		bool done = false;
		for(unsigned int i=0; i<trackers.size() && !done; i++)
		{
			if(trackers[i]->serverName==serverName)
			{
				done = true;

				if(trackers[i]->data[sensorNumber] != nullptr)
				{
					delete trackers[i]->data[sensorNumber];
					trackers[i]->data[sensorNumber] = nullptr;
					trackers[i]->numInitializedTrackers--;
				}

				if(trackers[i]->numInitializedTrackers == 0)
				{
					delete trackers[i]->tracker;
					delete trackers[i];
					trackers.erase(trackers.begin() + i);	
				}
			}
		}
	}

	EXPORT void RemoveButton(const char* serverName, int buttonNumber)
	{
		bool done = false;
		for(unsigned int i=0; i<buttons.size() && !done; i++)
		{
			if(buttons[i]->serverName==serverName)
			{
				done = true;

				if(buttons[i]->data[buttonNumber] != nullptr)
				{
					delete buttons[i]->data[buttonNumber];
					buttons[i]->data[buttonNumber] = nullptr;
					buttons[i]->numInitializedButtons--;
				}

				if(buttons[i]->numInitializedButtons == 0)
				{
					delete buttons[i]->button;
					delete buttons[i];
					buttons.erase(buttons.begin() + i);	
				}
			}
		}
	}

	EXPORT void RemoveAnalog(const char* serverName, int channelNumber)
	{
		bool done = false;
		for(unsigned int i=0; i<analogs.size() && !done; i++)
		{
			if(analogs[i]->serverName==serverName)
			{
				done = true;

				if(analogs[i]->data[channelNumber] != nullptr)
				{
					delete analogs[i]->data[channelNumber];
					analogs[i]->data[channelNumber] = nullptr;
					analogs[i]->numInitializedChannels--;
				}

				if(analogs[i]->numInitializedChannels == 0)
				{
					delete analogs[i]->analog;
					delete analogs[i];
					analogs.erase(analogs.begin() + i);	
				}
			}
		}
	}

	EXPORT void LockTrackerData(TrackerData* trackerData)
	{
		trackerData->mtx.lock();
	}

	EXPORT void UnlockTrackerData(TrackerData *trackerData)
	{
		trackerData->mtx.unlock();
	}
}