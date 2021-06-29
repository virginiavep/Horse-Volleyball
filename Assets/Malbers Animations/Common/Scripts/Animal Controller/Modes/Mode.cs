using MalbersAnimations.Events;
using MalbersAnimations.Scriptables;
using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using UnityEngine.Events;

namespace MalbersAnimations.Controller
{
    [System.Serializable] //DO NOT REMOVE!!!!!!!!!
    public class Mode
    {
        #region Public Variables
        /// <summary>Is this Mode Active?</summary>
        [SerializeField] private bool active = true;

        [SerializeField] private bool ignoreLowerModes = false; 

        /// <summary>Animation Tag Hash of the Mode</summary>
        protected int ModeTagHash;
        /// <summary>Which Input Enables the Ability </summary>
        public string Input;
        /// <summary>ID of the Mode </summary>
        [SerializeField] public ModeID ID;
        /// <summary>Modifier that can be used when the Mode is Enabled/Disabled or Interrupted</summary>
        public ModeModifier modifier;

        /// <summary>Elapsed time to be interrupted by another Mode If 0 then the Mode cannot be interrupted by any other mode </summary>
        public FloatReference CoolDown = new FloatReference(0);

        /// <summary>List of Abilities </summary>
        public List<Ability> Abilities;
        /// <summary>Active Ability index</summary>
        [SerializeField] private IntReference m_AbilityIndex = new IntReference(-99);
        public IntReference DefaultIndex = new IntReference(0);
        public IntEvent OnAbilityIndex = new IntEvent();
        public bool ResetToDefault = false;

        [SerializeField] private bool allowRotation = false;
        [SerializeField] private bool allowMovement = false;

        public UnityEvent OnEnterMode = new UnityEvent();
        public UnityEvent OnExitMode = new UnityEvent();

        #endregion

        #region Properties

        /// <summary>Is THIS Mode Playing?</summary>
        public bool PlayingMode { get; set; }
 
        /// <summary>Priority of the Mode.  Higher value more priority</summary>
        public int Priority { get; internal set; }

        /// <summary>Allows Additive rotation while the mode is playing </summary>
        public bool AllowRotation { get => allowRotation; set => allowRotation = value; }

        /// <summary>Allows Additive Speeds while the mode is playing </summary>
        public bool AllowMovement { get => allowMovement; set => allowMovement = value; }
       
        public string Name => ID != null ? ID.name : string.Empty;

        /// <summary>Does this Mode uses Cool Down? False if Cooldown = 0</summary>
        public bool HasCoolDown => CoolDown != 0;

        /// <summary>Is this mode in CoolDown?</summary>
        public bool InCoolDown   { get; internal set; }
       // public bool InCoolDown => !MTools.ElapsedTime(ModeActivationTime, CoolDown); //  { get; internal set; }

        public float ModeActivationTime;

        /// <summary>If enabled, it will play this Mode even if a Lower Mode is Playing </summary>
        public bool IgnoreLowerModes { get => ignoreLowerModes; set => ignoreLowerModes = value; }

        /// <summary> Active Ability Index of the mode</summary>
        public int AbilityIndex
        {
            get => m_AbilityIndex;  
            set
            {
                m_AbilityIndex.Value = value;
                OnAbilityIndex.Invoke(value);
            }   
        }

        public void SetAbilityIndex(int index) => AbilityIndex = index; 
        
         
        public MAnimal Animal { get; private set; }

        /// <summary> Current Selected Ability to Play on the Mode</summary>
        public Ability ActiveAbility { get; private set; }

        /// <summary>Current Value of the Input if this mode was called  by an Input</summary>
        public bool InputValue { get; internal set; }


        #endregion

        #region EditorVars
        public bool showGeneral = true;
        public bool showIndex = true;
        public bool showProperties = true;

        #endregion

        /// <summary>Set everyting up when the Animal Script Start</summary>
        public virtual void AwakeMode(MAnimal animal)
        {
            Animal = animal;                                    //Cache the Animal
            OnAbilityIndex.Invoke(AbilityIndex);                //Make the first invoke
            //InCoolDown = false;
            ModeActivationTime = -CoolDown*2;
        }

        /// <summary>Exit the current mode an ability</summary> 
        public virtual void ResetMode()
        {
            if (Animal.ActiveMode == this) //if is the same Mode then set the AnimalPlaying mode to false
            {
                Animal.Set_State_Sleep_FromMode(false);  //Restore all the States that are sleep from this mode

                if (Animal.ModeStatus != MStatus.Interrupted && Animal.ModeStatus != MStatus.ForceExit)
                    Animal.ModeStatus = MStatus.Completed;
            }

            PlayingMode = false;

            modifier?.OnModeExit(this);

            OnExitInvoke();

            ActiveAbility = null;                           //Reset to the default
        }

        /// <summary>Resets the Ability Index on the  animal to the default value</summary>
        public virtual void ResetAbilityIndex()
        {
            if (!Animal.Zone) SetAbilityIndex(DefaultIndex); //Dont reset it if you are on a zone... the Zone will do it automatically if you exit it
        }

        /// <summary>Returns True if a mode has an Ability Index</summary>
        public bool HasAbilityIndex(int index) => Abilities.Find(ab => ab.Index == index) != null;

        public void SetActive(bool value) => Active = value;

        public void ActivatebyInput(bool Input_Value)
        {
            if (!Active) return;
            if (!Animal.enabled) return;
            if (Animal.LockInput) return;               //Do no Activate if is sleep or disable or lock input is Enable;

            if (InputValue != Input_Value)              //Only Change if the Inputs are Different
            {
                InputValue = Input_Value;

               // MDebug($"Input: <B>[{Input_Value}]</B>");

                if (InputValue)
                {
                    var zone = Animal.Zone;

                    if (zone && zone.zoneType == ZoneType.Mode) //meaning the Zone its a Mode zone and it also changes the Status
                    {
                        var PreAbility = GetTryAbility(AbilityIndex);
                        if (PreAbility != null)
                        {
                            PreAbility.Status = (AbilityStatus) zone.m_abilityStatus;

                            if (PreAbility.Status == AbilityStatus.ActiveByTime) 
                                PreAbility.Properties.HoldByTime = zone.AbilityTime; //Set the Time for an Active by time Mode

                            if (TryActivate(PreAbility))
                            {
                                Animal.Mode_SetPower(Animal.Zone.ModeFloat); //Set the correct Mode Power Value
                                Animal.Zone.OnZoneActive(Animal);
                            }
                        }
                    }
                    else
                    TryActivate();
                }
                else
                {
                    if (PlayingMode && CheckStatus(AbilityStatus.HoldInputDown)) //if this mode is playing && is set to Hold by Input & the Input was true
                    { 
                        Animal.Mode_Interrupt();
                        MDebug($"<Interrupt> [Hold] Input = False");
                    }
                }
            }
        }


        /// <summary>Checks if the ability can be activated</summary>
        public virtual bool TryActivate(Ability newAbility)
        {
            int modestatus = 0; //Default Mode Status on the Mode .. This is changed if It can transition from an old ability to another

            if (newAbility == null)
            {
                MDebug($"There's no Ability with the Index: <B>{newAbility}</B> on the Mode <B> {Name}</B>");
                return false;
            }

            if (!newAbility.Active)
            {
                MDebug($"<B>[{newAbility.Name}]</B> is Disabled. Mode Ignored");
                return false;
            }

            if (StateCanInterrupt(Animal.ActiveState.ID, newAbility))       //Check if the States can block the mode
            {
                MDebug($"<B>[{newAbility.Name}]</B> cannot be Activated. The Active State won't allow it");
                return false; 
            }

            if (PlayingMode) //if is set to Toggle then if is already playing this mode then stop it
            {
                if (ActiveAbility.Index == newAbility.Index && CheckStatus(AbilityStatus.Toggle))
                {
                    InputValue = false;                     //Reset the Input Value to false of this mode
                    Animal.Mode_Interrupt();
                    MDebug("Toggle Off");
                    return false;
                }
                else if (newAbility.HasTransitionFrom && newAbility.Properties.TransitionFrom.Contains(ActiveAbility.Index)) //Means it can transition from one ability to another
                {
                    modestatus = ActiveAbility.Index; //This is used to Transition from an Older Mode Ability to a new one

                    ResetMode();
                    ModeExit(); //IMPORTANT  the Mode needs to be RESETED TO ENTER A NEW ABILITY

                    MDebug($"Transition with a : {newAbility.Name} can be done from the last Playing Ability");
                }

                if (!HasCoolDown) return false; 
            }
            else if (Animal.IsPlayingMode)   // Means the there's a Mode Playing
            {
                var AnimalMode = Animal.ActiveMode;         //Check if the animal is playin another mode
              
                if (Priority > AnimalMode.Priority && IgnoreLowerModes && !InCoolDown)
                {
                    AnimalMode.ResetMode();
                    AnimalMode.InputValue = false;  //Set the Input to false so both modes don't overlap
                    ModeExit(); //This allows to Play a mode again
                    MDebug($"Last Mode Interrupted, it Has lower Priority");
                }
                else
                {
                    if (!AnimalMode.HasCoolDown)
                    {
                        MDebug($"It Does not have CoolDown, Needs to finish the Current Mode");
                        return false; //Means that the Animations needs to finish first if the Active Mode Has no Cool Down so skip the code
                    }

                    if (!AnimalMode.InCoolDown)   //Means that the Active mode can be Interrupted since is no longer on cooldown
                    {
                        AnimalMode.ResetMode();
                        ModeExit(); //This allows to Play a mode again INT ID  = 0 to it can be available again
                    }
                  // return false;
                }
            }

            if (InCoolDown)
            {
                MDebug($"Current Mode in Cooldown. Cannot be activated");
                return false;                     //Exit the Mode After the cooldown has Passed no matter if is NOT PLAYING A MODE *GOOOD CODE
            }

            Activate(newAbility, modestatus);

            return true;
        }

        public void CheckActiveMode()
        {
            if (HasCoolDown && !InCoolDown && InputValue)   //Means that the Active mode can be Interrupted since is no longer on cooldown
            {
                ResetMode();
                ModeExit();   //This allows to Play a mode again INT ID  = 0 to it can be available again
            }
        }

        public virtual Ability GetTryAbility(int index)
        {
            if (!Active) return null;                //If the mode is Disabled Ignore
            this.AbilityIndex = index;
            modifier?.OnModeEnter(this);              //Check first if there's a modifier on Enter. Some mdifiers it will change the ABILITY INDEX...IMPORTANT 

            if (AbilityIndex == 0) return null;      //Means that no Ability is Active
            if (Animal.IsPreparingMode) return null; //Meaning that has already try to activate another Mode
            

            if (Abilities == null || Abilities.Count == 0)
            {
                MDebug("There's no Abilities Please set a list of Abilities");
                return null;
            }

            //Set the Index of the Ability for the Mode, Check for Random
            int NewIndex = (AbilityIndex == -99) ?
                Abilities[Random.Range(0, Abilities.Count)].Index.Value : 
                AbilityIndex;


            return  GetAbility(NewIndex); //Find the Ability
        }

        public virtual bool TryActivate() => TryActivate(AbilityIndex);

        public virtual bool TryActivate(int index)
        {
            var TryNextAbility = GetTryAbility(index);
            return TryActivate(TryNextAbility);
        }

        public virtual bool TryActivate(int index, AbilityStatus status, float time = 0)
        {
            var TryNextAbility = GetTryAbility(index);

            if (TryNextAbility != null)
            {
                TryNextAbility.Status = status;

                if (status == AbilityStatus.PlayOneTime) 
                    TryNextAbility.Properties.HoldByTime = time;

                return TryActivate(TryNextAbility);
            }
            return false;
        }




        /// <summary> Returns an ability by its Index </summary>
        public Ability GetAbility(int NewIndex) => Abilities.Find(item => item.Index == NewIndex);

        /// <summary> Returns an ability by its Name </summary>
        public Ability GetAbility(string abilityName) => Abilities.Find(item => item.Name == abilityName);

        public bool ForceActivate(int abilityIndex)
        {
            if (Animal.ActiveMode != null && !IsInTransition && !Animal.IsPreparingMode)
            {
                MDebug($"</B> with Ability: <B>{Animal.ActiveMode.ActiveAbility.Name} <color=black> [FORCE TO EXIT] </color> </B>");

                Animal.ModeStatus = MStatus.ForceExit;
                Animal.ActiveMode.ResetMode();
                ModeExit();           //This allows to Play a mode again
                AbilityIndex = abilityIndex;
            }
            return TryActivate();
        }

        /// <summary>Randomly Activates an Ability from this mode</summary>
        private void Activate(Ability newAbility, int modeStatus)
        {
            ActiveAbility = newAbility;
            MDebug($"Ability: <B>{ActiveAbility.Name}<color=yellow> [PREPARED] </color> </B>");
            Animal.SetModeParameters(this, modeStatus);
        }

        /// <summary>
        /// Called by the Mode Behaviour on Entering the Animation State 
        /// Done this way to check for Modes that are on other Layers besides the Base Layer </summary>
        public void AnimationTagEnter()
        {
            if (ActiveAbility != null && !PlayingMode)
            {
                Animal.ActiveMode = this;
                PlayingMode = true;

                Animal.Set_State_Sleep_FromMode(true);                      //Put to sleep the states needed
                Animal.ModeStatus = MStatus.Playing;
                OnEnterInvoke();                                        //Invoke the ON ENTER Event

                ModeActivationTime = Time.time; //Store the time the Mode started

                AbilityStatus AMode = ActiveAbility.Properties.Status; //Check if the Current Ability overrides the global properties

                int ModeStatus = Int_ID.Loop;    //That means the Ability is Loopable

                if (AMode == AbilityStatus.PlayOneTime)
                {
                    ModeStatus = Int_ID.OneTime;                //That means the Ability is OneTime 
                }
                else if (AMode == AbilityStatus.ActiveByTime)
                {
                    float HoldByTime = ActiveAbility.Properties.HoldByTime;
                    
                    Animal.StartCoroutine(Ability_By_Time(HoldByTime));
                    InputValue = false;
                }
                else if(AMode == AbilityStatus.Toggle)
                {
                    InputValue = false;
                }

                MDebug($"Mode: <B>{Name}<color=Green> [ANIMATION ENTER] </color></B>  with Ability: <B> {ActiveAbility.Name}</B> [{AMode}]");

                if (HasCoolDown) Animal.StartCoroutine(C_SetCoolDown(CoolDown));

                Animal.SetModeStatus(ModeStatus);
            }
        }

        /// <summary>Called by the Mode Behaviour on Exiting the  Animation State 
        /// Done this way to check for Modes that are on other Layers besides the base one </summary>
        public void AnimationTagExit(Ability exitingAbility, int exitAbility)
        {
            if (Animal.ActiveMode == this && ActiveAbility == exitingAbility)               //Means that we just exiting the same animation that we entered IMPORTANT
            {
                MDebug($"Mode: <B>{Name}<color=red> [ANIMATION EXIT] </color> </B>  with Ability <b>[{(exitingAbility?.Name)}] {Animal.ModeStatus} </b>" );
               
                ResetMode();
                ModeExit();

                if (exitAbility != -1)   //Meaning it will end in another mode
                { 
                    IsInTransition = false; //Reset that is in transition IMPORTANT

                    AbilityIndex = exitAbility;


                    if (TryActivate())
                    {
                       AnimationTagEnter();  //Do the animation Tag Enter since the next animation it may not be a entering mode animation
                    }
                }
            }
        }

        void ModeExit()
        {
            Animal.ActiveMode = null;
            Animal.SetModeStatus(Animal.ModeAbility = Int_ID.Available);
            Animal.ModeTime = 0;                            //Reset Mode Time 

            if (InputValue) TryActivate(); //Check if the Input is still Active so the mode can be reactivated again.
        }


        /// <summary> Is the Mode In transition </summary>
        public bool IsInTransition { get; set; }

        /// <summary> Is the Mode Active</summary>
        public bool Active { get => active; set => active = value; }

        public virtual void OnModeStateMove(AnimatorStateInfo stateInfo, Animator anim, int Layer)
        {
            IsInTransition = anim.IsInTransition(Layer) && 
            (anim.GetNextAnimatorStateInfo(Layer).fullPathHash != anim.GetCurrentAnimatorStateInfo(Layer).fullPathHash);
          
            if (Animal.ActiveMode == this)
            {
                Animal.ModeTime = stateInfo.normalizedTime;
                modifier?.OnModeMove(this, stateInfo, anim, Layer);
            }
        }

        /// <summary> Check for Exiting the Mode, If the animal changed to a new state and the Affect list has some State</summary>
        public virtual bool StateCanInterrupt(StateID ID, Ability ability = null)
        {
            if (ability == null) ability = ActiveAbility;

            var properties = ability.Properties;

            if (properties.affect == AffectStates.None) return false;

            if (ability.HasAffectStates)
            {
                if (properties.affect == AffectStates.Exclude && HasState(properties,ID)      //If the new state is on the Exclude State
                || (properties.affect == AffectStates.Include && !HasState(properties,ID)))   //OR If the new state is not on the Include State
                {
                    MDebug($"Current State [{ID.name}] is Blocking <B>" + ability.Name + "</B>");

                    return true;
                }
            }
            return false;
        }

        /// <summary>Find if a State ID is on the Avoid/Include Override list</summary>
        protected static bool HasState(ModeProperties properties, StateID ID) => properties.affectStates.Exists(x => x.ID == ID.ID);


        public IEnumerator C_SetCoolDown(float time)
        {
            InCoolDown = true;
            yield return new WaitForSeconds(time);
            InCoolDown = false;

            if (InputValue) TryActivate(); //Check if the Input is still Active
        }




        protected IEnumerator Ability_By_Time(float time)
        {
            MDebug("ActiveByTime: " + time);

            yield return new WaitForSeconds(time);
            Animal.Mode_Interrupt();
        }

 

        private void OnExitInvoke()
        {
            ActiveAbility.Properties.OnExit.Invoke();
            OnExitMode.Invoke();
        }

        private void OnEnterInvoke()
        {
            ActiveAbility.Properties.OnEnter.Invoke();
            OnEnterMode.Invoke();
        }


        private bool CheckStatus(AbilityStatus status)
        {
            if (ActiveAbility == null) return false;
            return ActiveAbility.Properties.Status == status;
        }

        /// <summary>Disable the Mode. If the mode is playing it check the status and it disable it properly </summary>
        public virtual void Disable()
        {
            Active = false;
            InputValue = false;

            if (PlayingMode)
            {
                if (!CheckStatus(AbilityStatus.PlayOneTime))
                {
                    Animal.Mode_Interrupt();
                }
                else
                {
                    //Do nothing ... let the mode finish since is on AbilityStatus.PlayOneTime
                }
            }
        }

        public virtual void Enable() => Active = true;


        private void MDebug(string deb)
        {
#if UNITY_EDITOR
            if (Animal.debugModes) Debug.Log($"Mode <b>{ID.name}</b> - {deb}");
#endif
        }

    }
    /// <summary> Ability for the Modes</summary>
    [System.Serializable]
    public class Ability
    {
        /// <summary>Is the Ability Active</summary>
        public BoolReference active = new BoolReference(true);
        /// <summary>Name of the Ability (Visual Only)</summary>
        public string Name;
        /// <summary>index of the Ability </summary>
        public IntReference Index =  new IntReference(0);
     
        /// <summary>Overrides Properties on the mode</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("OverrideProperties")]
        public ModeProperties Properties;

        /// <summary>Status of the Ability (Play One Time, Forever, by time)</summary>
        public AbilityStatus Status { get => Properties.Status; set => Properties.Status = value; }

        /// <summary>Time value when the Status is set Time</summary>
        public float  AbilityTime { get => Properties.HoldByTime; set => Properties.HoldByTime= value; }

        /// <summary>It Has Affect states to check</summary>
        public bool HasAffectStates => Properties.affectStates != null && Properties.affectStates.Count > 0;
        public bool HasTransitionFrom => Properties.TransitionFrom != null && Properties.TransitionFrom.Count > 0;

        public bool Active { get => active.Value; set => active.Value = value; }
    }

    public enum AbilityStatus
    {
        /// <summary> The Ability is Enabled One time and Exit when the Animation is finished </summary>
        PlayOneTime = 0,
        /// <summary> The Ability is On while the Input True</summary>
        HoldInputDown = 1,
        /// <summary> The Ability is On for an x ammount of time</summary>
        ActiveByTime = 2,
        /// <summary> The Ability is ON and OFF everytime the Activate method is called</summary>
        Toggle = 3,
        /// <summary> The Ability is Play forever until is Mode Interrupt is called</summary>
        Forever = 4,
    }
    public enum AffectStates
    {
        None,
        Include,
        Exclude,
    }

    [System.Serializable]
    public class ModeProperties
    {
        /// <summary>The Ability can Stay Active until it finish the Animation, by Holding the Input Down, by x time </summary>
        [Tooltip("The Ability can Stay Active until it finish the Animation, by Holding the Input Down, by x time ")]
        public AbilityStatus Status = AbilityStatus.PlayOneTime;
        
        /// <summary>The Ability can Stay Active by x seconds </summary>
        [Tooltip("The Ability can Stay Active by x seconds")]
        public FloatReference HoldByTime = new FloatReference(0);
   
        ///// <summary>If Exlude then the Mode will not be Enabled when is on a State on the List, If Include, then the mode will only be active when the Animal is on a state on the List </summary>
        [Tooltip("If Exlude then the Mode will not be Enabled when is on a State on the List, If Include, then the mode will only be active when the Animal is on a state on the List")]
        public AffectStates affect = AffectStates.None;
        /// <summary>Include/Exclude the  States on this list depending the Affect variable</summary>
        [Tooltip("Include/Exclude the  States on this list depending the Affect variable")]
        public List<StateID> affectStates = new List<StateID>();

        [Tooltip("Modes can transition from other abilities inside the same mode. E.g Seat -> Lie -> Sleep")]
        public List<int> TransitionFrom = new List<int>();

        [SerializeField] private bool ShowEvents;
        public UnityEvent OnEnter;
        public UnityEvent OnExit;

        public ModeProperties(ModeProperties properties)
        {
            ShowEvents = properties.ShowEvents;
            Status = properties.Status;
            affect= properties.affect;
            HoldByTime= properties.HoldByTime.Value;
            affectStates =  new List<StateID>(properties.affectStates);
            TransitionFrom = new List<int>();
        }
    }
}