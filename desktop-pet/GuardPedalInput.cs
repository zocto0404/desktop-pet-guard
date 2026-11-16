namespace WobblyPet;

sealed class GuardPedalInput
{
    PedalFrame? previous;

    public bool Step(GuardGame game, PedalFrame? frame, double dt)
    {
        var last = previous;
        previous = frame;
        if (frame is not { Ready: true, HasMotion: true })
        {
            game.LeftFoot.Pending = game.RightFoot.Pending = false;
            return false;
        }

        if (!game.Paused && game.State == "playing")
        {
            var sameStream = last is { Ready: true, HasMotion: true } && last.Session == frame.Session;
            Apply(true, frame.Left, sameStream ? last!.Left.Stomps : frame.Left.Stomps);
            Apply(false, frame.Right, sameStream ? last!.Right.Stomps : frame.Right.Stomps);
        }
        return true;

        void Apply(bool left, PedalFoot foot, uint lastStomp)
        {
            if (foot.Contact != PedalContact.On) return;
            game.Move(left, foot.X, foot.Y, dt);
            if (foot.Stomps <= lastStomp) return;
            // The simulation still waits for the slipper to land before checking hits.
            game.Press(left);
            game.Release(left);
        }
    }
}
