using System;

namespace Scalar.Common
{
    public class GitCommandLineParser
    {
        private const int GitIndex = 0;
        private const int VerbIndex = 1;
        private const int ArgumentsOffset = 2;

        private readonly string[] parts;
        private Verbs commandVerb;

        public GitCommandLineParser(string command)
        {
            if (!string.IsNullOrWhiteSpace(command))
            {
                this.parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (this.parts.Length < VerbIndex + 1 ||
                    this.parts[GitIndex] != "git")
                {
                    this.parts = null;
                }
                else
                {
                    this.commandVerb = this.StringToVerbs(this.parts[VerbIndex]);
                }
            }
        }

        [Flags]
        public enum Verbs
        {
            Other = 1 << 0,
            AddOrStage = 1 << 1,
            Checkout = 1 << 2,
            Commit = 1 << 3,
            Move = 1 << 4,
            Reset = 1 << 5,
            Status = 1 << 6,
            UpdateIndex = 1 << 7,
        }

        public bool IsValidGitCommand
        {
            get { return this.parts != null; }
        }

        public bool IsResetSoftOrMixed()
        {
            return
                this.IsVerb(Verbs.Reset) &&
                !this.HasArgument("--hard") &&
                !this.HasArgument("--keep") &&
                !this.HasArgument("--merge");
        }

        public bool IsSerializedStatus()
        {
            return this.IsVerb(Verbs.Status) &&
                this.HasArgumentPrefix("--serialize");
        }

        public bool IsVerb(Verbs verbs)
        {
            if (!this.IsValidGitCommand)
            {
                return false;
            }

            return (verbs & this.commandVerb) == this.commandVerb;
        }

        private Verbs StringToVerbs(string verb)
        {
            switch (verb)
            {
                case "add": return Verbs.AddOrStage;
                case "checkout": return Verbs.Checkout;
                case "commit": return Verbs.Commit;
                case "mv": return Verbs.Move;
                case "reset": return Verbs.Reset;
                case "stage": return Verbs.AddOrStage;
                case "status": return Verbs.Status;
                case "update-index": return Verbs.UpdateIndex;
                default: return Verbs.Other;
            }
        }

        private bool HasArgument(string argument)
        {
            return this.HasAnyArgument(arg => arg == argument);
        }

        private bool HasArgumentPrefix(string argument)
        {
            return this.HasAnyArgument(arg => arg.StartsWith(argument, StringComparison.Ordinal));
        }

        private bool HasArgumentAtIndex(string argument, int argumentIndex)
        {
            int actualIndex = argumentIndex + ArgumentsOffset;
            return
                this.parts.Length > actualIndex &&
                this.parts[actualIndex] == argument;
        }

        private bool HasAnyArgument(Predicate<string> argumentPredicate)
        {
            int argumentIndex;
            return this.HasAnyArgument(argumentPredicate, out argumentIndex);
        }

        private bool HasAnyArgument(Predicate<string> argumentPredicate, out int argumentIndex)
        {
            argumentIndex = -1;

            if (!this.IsValidGitCommand)
            {
                return false;
            }

            for (int i = ArgumentsOffset; i < this.parts.Length; i++)
            {
                if (argumentPredicate(this.parts[i]))
                {
                    argumentIndex = i - ArgumentsOffset;
                    return true;
                }
            }

            return false;
        }
    }
}
