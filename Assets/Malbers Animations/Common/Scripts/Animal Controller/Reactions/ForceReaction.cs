using MalbersAnimations.Scriptables;
using System.Collections;
using UnityEngine;

namespace MalbersAnimations.Controller.Reactions
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Malbers Animations/Animal Reactions/Force Reaction", order = 10)]
    public class ForceReaction : MReaction
    {
        public enum DirectionType { Local, World}


        [Tooltip("Relative Direction of the Force to apply")]
        public Vector3 Direction =  Vector3.forward;
        [Tooltip("Direction mode to be applied the force on the Animal. World, or Local")]
        public DirectionType Mode = DirectionType.Local;

        [Tooltip("Time to Apply the force")]
        public FloatReference time = new FloatReference(1f);
        [Tooltip("Amount of force to apply")]
        public FloatReference force = new FloatReference( 10f);
        [Tooltip("Aceleration to apply to the force")]
        public FloatReference Aceleration = new FloatReference( 2f);
        [Tooltip("Drag to Decrease the Force after the Force time has pass")]
        public FloatReference ExitDrag = new FloatReference(2f);
        [Tooltip("Set if the Animal is grounded when adding a force")]
        public BoolReference Grounded = new BoolReference(false);

        protected override void _React(MAnimal animal)
        {
            if (animal.enabled && animal.gameObject.activeInHierarchy)  animal.StartCoroutine(IForceC(animal));
        }

        IEnumerator IForceC(MAnimal animal)
        {
            var Force = force * Direction.normalized;

            if (Mode == DirectionType.Local) 
                Force = animal.transform.TransformDirection(Force);

            var StartExtForce = animal.CurrentExternalForce + animal.GravityStoredVelocity; //Calculate the Starting force
            animal.CurrentExternalForce = StartExtForce;

            animal.Grounded = Grounded;

            animal.ExternalForce = Force;
            animal.ExternalForceAcel = Aceleration;

            if (animal.ActiveState.ID == StateEnum.Fall) //If we enter to a zone from the Fall state.. Reset the Fall Current Distance
            {
                var fall = animal.ActiveState as Fall;
                fall.FallCurrentDistance = 0;
            }

            yield return new WaitForSeconds(time);

            if (animal.ActiveState.ID == StateEnum.Fall) animal.UseGravity = true;  //If we are on the Fall State -- Reactivate the Gravity

            if (ExitDrag > 0) //
            {
                animal.ExternalForceAcel = ExitDrag;
                animal.ExternalForce = Vector3.zero;
            }


        }


        protected override bool _TryReact(MAnimal animal)
        {
            _React(animal);
            return true;
        }
 
      
        private const string reactionName = "Force → ";
      
        
       
    }
}
