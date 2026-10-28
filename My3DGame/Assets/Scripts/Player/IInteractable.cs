// ============================================================
// IInteractable.cs
// ------------------------------------------------------------
// Any GameObject that the player can interact with (E key)
// must implement this interface.  The PlayerInteractor script
// does a raycast and checks for this interface; it never needs
// to know the concrete type of the object it hits.
//
// How to use:
//   1. Create a MonoBehaviour on an object (e.g. Door.cs).
//   2. Make it implement IInteractable.
//   3. Fill in GetPrompt() and Interact().
// ============================================================

namespace TimeFracture.Player
{
    public interface IInteractable
    {
        // Returns the text shown in the centre-screen prompt
        // while the player is looking at this object.
        // Example: "Press [E] to open door"
        string GetPrompt();

        // Called when the player presses E while looking at
        // this object.  The interactor is passed in so the
        // object can read data such as the player's position
        // if needed (rarely necessary in Milestone 1).
        void Interact(PlayerInteractor interactor);
    }
}
