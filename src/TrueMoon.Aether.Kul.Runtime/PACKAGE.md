# TrueMoon.Aether.Kul.Runtime

Explicit App adapter for nonspatial sound and streamed music in Aether game sessions.
Register UseAudio, UseAetherKul(capturedContent), and AddKulAudio in the shared subsystem builder.
Each scene owns prepared, initially silent voices and immutable content pins. Ticks perform
transport controls only. The App owns the audio device; the scene borrows it through a caller-owned session.
