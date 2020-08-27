using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.AudioDistribution;         // For the 6x100 Amp
using Crestron.SimplSharpPro.EthernetCommunication;     // For the EISC

namespace P_197_Take_1 {

    public class ControlSystem : CrestronControlSystem {

        // Define Devices and Stuff

        private C2nAmp6X100 amp197;

        private Room[] p197zones;

        private EthernetIntersystemCommunications ampeisc;

        private uint[] screens;
        private ushort[] sources;

        public ControlSystem()
            : base() {

            try {

                Thread.MaxNumberOfUserThreads = 20;

                // Register the Amp, the Zones and the EISC

                // Amp Construction

                amp197 = new C2nAmp6X100(0x04, this);
                amp197.Description = "~~ P-197 Amplifier ~~";
                amp197.RoomChangeEvent += new RoomEventHandler(amp197_RoomChangeEvent);
                amp197.Register();

                // Zones Definition

                p197zones = new Room[7];

                p197zones[0] = amp197.Room[1];          // Master Bed
                p197zones[1] = amp197.Room[2];          // Master Ensuite
                p197zones[2] = amp197.Room[3];          // Bedroom 2
                p197zones[3] = amp197.Room[4];          // Ensuite 2
                p197zones[4] = amp197.Room[5];          // Bedroom 3
                p197zones[5] = amp197.Room[7];          // Kitchen
                p197zones[6] = amp197.Room[8];          // Dining

                amp197.Room[1].Name.StringValue = "Master Bed";
                amp197.Room[2].Name.StringValue = "Master Ensuite";
                amp197.Room[3].Name.StringValue = "Bedroom 2";
                amp197.Room[4].Name.StringValue = "Ensuite 2";
                amp197.Room[5].Name.StringValue = "Bedroom 3";
                amp197.Room[7].Name.StringValue = "Kitchen";
                amp197.Room[8].Name.StringValue = "Dining";

                // Sources Array

                sources = new ushort[7];

                sources[0] = 1;
                sources[1] = 1;
                sources[2] = 2;
                sources[3] = 2;
                sources[4] = 3;
                sources[5] = 4;
                sources[6] = 4;

                // Initialize Every Zone Volume to a non eardrum-fatal level

                for (uint i = 0; i <= 6; i++)
                    p197zones[i].Volume.UShortValue = p197zones[i].VolumeFeedback.UShortValue;

                // EISC Construction

                ampeisc = new EthernetIntersystemCommunications(0xf0, "127.0.0.2", this);
                ampeisc.Description = "~~ Amplifier EISC ~~";
                ampeisc.SigChange += new SigEventHandler(ampeisc_SigChange);
                ampeisc.Register();

                // Cross Routing

                screens = new uint[6];
                for (uint i = 0; i <= 5; i++)
                    screens[i] = 33;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);
            }
            catch (Exception e) {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }




        /* This is a function that refreshes the feedbacks for the cross matrix */

        void RefreshTSFbs(uint x) {
            ampeisc.UShortInput[x + 1].UShortValue = p197zones[screens[x]].VolumeFeedback.UShortValue;
            ampeisc.UShortInput[x + 7].UShortValue = (ushort)(Math.Floor((double)(p197zones[screens[x]].VolumeFeedback.UShortValue) / 655));
            ampeisc.BooleanInput[x + 1].BoolValue = p197zones[screens[x]].MuteOnFeedback.BoolValue;
        }



        /* This is the handler for our EISC, here we define the use of our signals, the control of the amp . . . */

        void ampeisc_SigChange(BasicTriList currentDevice, SigEventArgs args) {

            switch (args.Sig.Type) {            // Check the type of signal changing

                case eSigType.Bool:         // If the signal is digital

                    if (args.Sig.BoolValue) {           // If it is high

                        if (args.Sig.Number > 0 && args.Sig.Number <= 42) {         // Control TSx -> Ry

                            if (args.Sig.Number > 0 && args.Sig.Number <= 7) {          // Control TS1 -> R1-7
                                screens[0] = args.Sig.Number - 1;
                                RefreshTSFbs(0);
                            }

                            else if (args.Sig.Number > 7 && args.Sig.Number <= 14) {            // Control TS2 -> R1-7
                                screens[1] = args.Sig.Number - 8;
                                RefreshTSFbs(1);
                            }

                            else if (args.Sig.Number > 14 && args.Sig.Number <= 21) {           // Control TS3 -> R1-7
                                screens[2] = args.Sig.Number - 15;
                                RefreshTSFbs(2);
                            }

                            else if (args.Sig.Number > 21 && args.Sig.Number <= 28) {           // Control TS4 -> R1-7
                                screens[3] = args.Sig.Number - 22;
                                RefreshTSFbs(3);
                            }

                            else if (args.Sig.Number > 28 && args.Sig.Number <= 35) {           // Control TS5 -> R1-7
                                screens[4] = args.Sig.Number - 29;
                                RefreshTSFbs(4);
                            }

                            else if (args.Sig.Number > 35 && args.Sig.Number <= 42) {           // Control TS6 -> R1-7
                                screens[5] = args.Sig.Number - 36;
                                RefreshTSFbs(5);
                            }
                        }

                        else if (args.Sig.Number > 42 && args.Sig.Number <= 48) {           // Disconnect TSx
                            screens[args.Sig.Number - 43] = 33;
                        }

                        else if (args.Sig.Number > 48 && args.Sig.Number <= 54) {           // TSx Room Power On
                            p197zones[screens[args.Sig.Number - 49]].Source.UShortValue = sources[screens[args.Sig.Number - 49]];
                        }

                        else if (args.Sig.Number > 54 && args.Sig.Number <= 60) {           // TSx Room Power Off
                            p197zones[screens[args.Sig.Number - 55]].Source.UShortValue = 0;
                        }

                        else if (args.Sig.Number > 60 && args.Sig.Number <= 66) {           // TSx Room Mute On/Off
                            if (screens[args.Sig.Number - 61] != 33) {
                                if (p197zones[screens[args.Sig.Number - 61]].MuteOnFeedback.BoolValue)
                                    p197zones[screens[args.Sig.Number - 61]].MuteOff();
                                else
                                    p197zones[screens[args.Sig.Number - 61]].MuteOn();
                            }
                            CrestronConsole.PrintLine("Percent Volume for TS1 is: {0}, and Volume Feedback for TS1 is: {1}", ampeisc.UShortInput[7].UShortValue, ampeisc.UShortInput[1].UShortValue);
                        }

                        else if (args.Sig.Number > 66 && args.Sig.Number <= 72) {           // TSx Room Volume Up
                            if (screens[args.Sig.Number - 67] != 33) {
                                if (p197zones[screens[args.Sig.Number - 67]].VolumeFeedback.UShortValue < 64880)
                                    p197zones[screens[args.Sig.Number - 67]].Volume.UShortValue = (ushort)(p197zones[screens[args.Sig.Number - 67]].VolumeFeedback.UShortValue + (ushort)655);
                            }
                        }

                        else if (args.Sig.Number > 72 && args.Sig.Number <= 78) {           // TSx Room Volume Down
                            if (screens[args.Sig.Number - 73] != 33) {
                                if (p197zones[screens[args.Sig.Number - 73]].VolumeFeedback.UShortValue > 655)
                                    p197zones[screens[args.Sig.Number - 73]].Volume.UShortValue = (ushort)(p197zones[screens[args.Sig.Number - 73]].VolumeFeedback.UShortValue - (ushort)655);
                            }
                        }

                        else if (args.Sig.Number > 78 && args.Sig.Number <= 85) {           // Zones Power On
                            p197zones[args.Sig.Number - 79].Source.UShortValue = sources[args.Sig.Number - 79];
                        }

                        else if (args.Sig.Number > 85 && args.Sig.Number <= 92) {           // Zones Power Off
                            p197zones[args.Sig.Number - 86].Source.UShortValue = 0;
                        }

                        else if (args.Sig.Number > 92 && args.Sig.Number <= 99) {           // Zones Mute On/Off
                            if (p197zones[args.Sig.Number - 93].MuteOnFeedback.BoolValue)
                                p197zones[args.Sig.Number - 93].MuteOff();
                            else
                                p197zones[args.Sig.Number - 93].MuteOn();
                        }

                        else if (args.Sig.Number > 99 && args.Sig.Number <= 106) {          // Zones Volume Up
                            if (p197zones[args.Sig.Number - 100].VolumeFeedback.UShortValue < 64880)
                                p197zones[args.Sig.Number - 100].Volume.UShortValue = (ushort)(p197zones[args.Sig.Number - 100].VolumeFeedback.UShortValue + (ushort)655);
                        }

                        else if (args.Sig.Number > 106 && args.Sig.Number <= 113) {         // Zones Volume Down
                            if (p197zones[args.Sig.Number - 107].VolumeFeedback.UShortValue > 655)
                                p197zones[args.Sig.Number - 107].Volume.UShortValue = (ushort)(p197zones[args.Sig.Number - 107].VolumeFeedback.UShortValue - (ushort)655);
                        }
                    }

                    break;          // break for case of digital signal


                case eSigType.UShort:           // If we got an analog signal

                    if (args.Sig.Number > 0 && args.Sig.Number <= 6) {          // TSx Volume Set
                        p197zones[screens[args.Sig.Number - 1]].Volume.UShortValue = args.Sig.UShortValue;
                    }

                    else if (args.Sig.Number > 6 && args.Sig.Number <= 13) {           // Zones Volume Set 
                        p197zones[args.Sig.Number - 7].Volume.UShortValue = args.Sig.UShortValue;
                    }

                    break;          // break for case of analog signal
            }
        }



        /* This is the handler for the Amplifier, here we define the Feedbacks whenever an event happens . . . */

        void amp197_RoomChangeEvent(object sender, RoomEventArgs args) {

            CrestronConsole.PrintLine("RoomEventArgs right now is: {0}, and Room Volume Event Id is: {1} ", args.EventId, Room.VolumeFeedbackEventId);

            switch (args.EventId) {             // IT DOESNT WORK, THE EVENTS NEVER HAPPEN?

                case Room.VolumeFeedbackEventId:            // When the Volume changes 

                    for (uint i = 1; i <= 6; i++) {           // Analog Outputs 1 - 6 are TSx Volume Feedback
                        if (screens[i - 1] != 33)
                            ampeisc.UShortInput[i].UShortValue = p197zones[screens[i - 1]].VolumeFeedback.UShortValue;
                    }

                    for (uint i = 7; i <= 12; i++) {            // Analog Outputs 7 - 12 are TSx Volume % Feedback
                        if (screens[i - 7] != 33)
                            ampeisc.UShortInput[i].UShortValue = (ushort)(Math.Floor((double)(p197zones[screens[i - 7]].VolumeFeedback.UShortValue) / 655));
                    }

                    CrestronConsole.PrintLine("no segmentation until here");

                    for (uint i = 13; i <= 19; i++) {           // Analog Outputs 13 - 19 are Zones Volume Feedback
                        ampeisc.UShortInput[i].UShortValue = p197zones[i - 13].VolumeFeedback.UShortValue;
                    }

                    for (uint i = 20; i <= 26; i++) {           // Analog Outputs 20 - 26 are Zones Volume % Feedback
                        ampeisc.UShortInput[i].UShortValue = (ushort)(Math.Floor((double)(p197zones[i - 20].VolumeFeedback.UShortValue) / 655));
                    }

                    CrestronConsole.PrintLine("Event Now");

                    break;          // break for case of volume feedback event

                case Room.MuteOnFeedbackEventId:            // If its Muted

                    for (uint i = 1; i <= 6; i++) {         // Digital Outputs 1 - 6 are TSx Mute On Feedback
                        if (screens[i - 1] != 33)
                            ampeisc.BooleanInput[i].BoolValue = p197zones[screens[i - 1]].MuteOnFeedback.BoolValue;
                    }

                    for (uint i = 7; i <= 13; i++) {            // Digital Outputs 7 - 13 are Zones Mute On Feedback
                        ampeisc.BooleanInput[i].BoolValue = p197zones[i - 7].MuteOnFeedback.BoolValue;
                    }

                    break;          // break for case of mute feedback event

                case Room.SourceFeedbackEventId:            // If there is a source change

                    for (uint i = 14; i <= 19; i++) {           // Digital Outputs 14 - 19 are TSx Room On Feedback
                        if (screens[i - 14] != 33) {
                            if (p197zones[screens[i - 14]].SourceFeedback.UShortValue != 0)
                                ampeisc.BooleanInput[i].BoolValue = true;
                            else
                                ampeisc.BooleanInput[i].BoolValue = false;
                        }
                    }

                    for (uint i = 20; i <= 26; i++) {           // Digital Outputs 20 - 26 are Zones Room On Feedback
                        if (p197zones[i - 20].SourceFeedback.UShortValue != 0)
                            ampeisc.BooleanInput[i].BoolValue = true;
                        else
                            ampeisc.BooleanInput[i].BoolValue = false;
                    }

                    break;          // break for case of source feedback event
            }
        }






        /* ---------------------------- SIGNAL DICTIONARY FOR THIS PROGRAM ------------------------------------------- */
        /* DIGITAL: Inputs 1 - 7: Control TS1 -> Rx
         *          Inputs 8 - 14: Control TS2 -> Rx
         *          Inputs 15 - 21: Control TS3 -> Rx
         *          Inputs 22 - 28: Control TS4 -> Rx
         *          Inputs 29 - 35: Control TS5 -> Rx
         *          Inputs 36 - 42: Control TS6 -> Rx
         *          Inputs 43 - 48: Disconnect TSx
         *          Inputs 49 - 54: TSx Power On
         *          Inputs 55 - 60: TSx Power Off
         *          Inputs 61 - 66: TSx Mute On/Off
         *          Inputs 67 - 72: TSx Room Volume Up
         *          Inputs 73 - 78: TSx Room Volume Down
         *          Inputs 79 - 85: Zones Power On
         *          Inputs 86 - 92: Zones Power Off
         *          Inputs 93 - 99: Zones Mute On/Off
         *          Inputs 100 - 106: Zones Volume Up
         *          Inputs 107 - 113: Zones Volume Down
         *          
         *          Outputs 1 - 6: TSx Mute On Feedback
         *          Outputs 7 - 13: Zones Mute On Feedback
         *          Outputs 14 - 19: TSx Room On Feedback
         *          Outputs 20 - 26: Zones Power On Feedback
         *          
         * ANALOG:  Inputs 1 - 6: TSx Volume Set
         *          Inputs 7 - 13: Zones Volume Set
         * 
         *          Outputs 1 - 6: TSx Volume Feedback
         *          Outputs 7 - 12: TSx Volume % Feedback
         *          Outputs 13 - 19: Zones Volume Feedback
         *          Outputs 20 - 26: Zones Volume % Feedback
         *          
         * --------------------------------------------- ENDS HERE ----------------------------------------------------*/













        /* ------------------------ Don't Touch Please ----------------------------- */

        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// 
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        public override void InitializeSystem() {
            try {

            }
            catch (Exception e) {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs) {
            switch (ethernetEventArgs.EthernetEventType) {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter) {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter) {

                    }
                    break;
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType) {
            switch (programStatusEventType) {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
            }

        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType) {
            switch (systemEventType) {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }
    }
}