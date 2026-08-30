using System;
using System.IO;
using System.Security;

namespace ImmortalLoot.Player
{
    public static class PlayerSaveAttempt
    {
        public static bool Execute(Action save, Action<Exception> onFailure = null)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            try
            {
                save();
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is SecurityException)
            {
                try { onFailure?.Invoke(exception); }
                catch (Exception)
                {
                    // Storage already failed; diagnostic UI/logging must never turn that failure into a stopped game loop.
                }
                return false;
            }
        }
    }
}
