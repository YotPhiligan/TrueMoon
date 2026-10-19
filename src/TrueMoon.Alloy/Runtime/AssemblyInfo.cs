using System.Runtime.CompilerServices;

// The optional host attaches its private wake signal without exposing callback execution to consumers.
[assembly: InternalsVisibleTo("TrueMoon.Alloy.Hosting")]
