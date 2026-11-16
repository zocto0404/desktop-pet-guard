using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;

namespace WobblyPet;

static class PedalTests
{
    const string Rest = "M1,1,0,0,0,1,0,0,0";
    static void Assert(bool pass,string message) { if(!pass)throw new Exception(message); }

    public static void Run(string folder)
    {
        var rig=new Rig();
        foreach(var (bits,expected) in new[]{("11",PetState.Normal),("10",PetState.Left),
                    ("01",PetState.Right),("00",PetState.Slime)})
        {
            var frame=PedalFrame.Parse(bits,1)!;
            rig.SetKeys(frame.Left.Contact==PedalContact.On,frame.Right.Contact==PedalContact.On);
            Assert(frame.Ready&&rig.State==expected&&!frame.HasMotion,"Changed daily pedal mapping");
        }
        foreach (var invalid in new[]{"M2,1,0,0,0,1,0,0,0","M1,1,NaN,0,0,1,0,0,0",
                     "M1,1,0,1.1,0,1,0,0,0","M1,1,0,0,-1,1,0,0,0","M1,11,0,0,0,1,0,0,0"})
            Assert(PedalFrame.Parse(invalid,1)==null,"Accepted malformed frame: "+invalid);

        var game=new GuardGame();
        var pedals=new GuardPedalInput();
        bool Input(string message,long session=1)=>pedals.Step(game,PedalFrame.Parse(message,session),.02);
        game.Aim(true,300,420);game.Aim(false,900,420);
        Assert(Input(Rest),"Both feet should be ready");
        Input("M1,1,0.8,-0.7,0,1,-0.3,0.5,0");
        Assert(game.LeftFoot.X>300&&game.LeftFoot.Y<420&&game.RightFoot.X<900&&game.RightFoot.Y>420,
            "Feet did not move independently in two axes");
        game.Aim(true,300,420);game.Aim(false,900,420);
        var left=new GuardActor{Id=0,Type=0,Position=new(300,420)};
        var right=new GuardActor{Id=1,Type=1,Position=new(900,420)};
        game.Add(left);game.Add(right);
        Input("M1,1,0,0,1,1,0,0,0");
        Assert(game.Hits==0,"Input hit on press instead of landing");
        for(int i=0;i<8;i++)game.Step(1d/60);
        Assert(left.Dead&&!right.Dead,"Left input crossed into the right foot");
        Input("M1,1,0,0,1,1,0,0,1");
        for(int i=0;i<8;i++)game.Step(1d/60);
        Assert(right.Dead&&game.Hits==2&&game.Rings.Count==2,"Right foot did not land independently");
        for(int i=0;i<37;i++)game.Step(1d/60);
        Input("M1,1,0,0,1,1,0,0,1");
        Assert(!game.LeftFoot.Pending&&!game.RightFoot.Pending,"Duplicate snapshot replayed a stomp");
        Input("M1,0,1,1,2,1,0,0,1");
        Assert(game.LeftFoot.X==300&&!game.LeftFoot.Pending,"An off-board foot moved or stomped");
        Input("M1,1,0,0,3,1,0,0,1");
        Assert(game.LeftFoot.Pending,"Stomp setup failed");
        Assert(!pedals.Step(game,null,.02)&&!game.LeftFoot.Pending,"Lost stream retained a pending strike");
        Input("M1,1,0,0,42,1,0,0,27",session:2);
        Assert(!game.LeftFoot.Pending&&!game.RightFoot.Pending,"Reconnect replayed old stomps");
        game.TogglePause();
        Input("M1,1,1,0,43,1,0,0,27",session:2);
        game.TogglePause();
        Input("M1,1,0,0,43,1,0,0,27",session:2);
        Assert(game.LeftFoot.X==300&&!game.LeftFoot.Pending,"Paused input moved the foot or replayed after resume");

        using(var bridge=new PedalBridge(15180)) CheckBridge(bridge).GetAwaiter().GetResult();
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"pedal-result.txt"),"PASS: four daily states; two independent axes and landings; off-board guard; pause; no delayed or duplicate stomp; reconnect; local page; token/origin authorization; acknowledgement; stale input.");
    }

    static async Task CheckBridge(PedalBridge bridge)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(7));
        var page=new Uri(bridge.PairingUrl);
        using var http=new HttpClient(new HttpClientHandler {UseProxy=false});
        string html=await http.GetStringAsync(page,deadline.Token).ConfigureAwait(false);
        Assert(html.Contains("desktop-pet-pedals"),"Local bridge did not serve its relay");
        var token=page.Fragment["#pet=".Length..];
        var ws=new Uri($"ws://{page.Authority}/pedals/?token={token}");
        await Reject(ws,"http://127.0.0.1:9999").ConfigureAwait(false);
        await Reject(new Uri($"ws://{page.Authority}/pedals/?token=wrong"),page.GetLeftPart(UriPartial.Authority)).ConfigureAwait(false);
        using var client=new ClientWebSocket();
        client.Options.SetRequestHeader("Origin",page.GetLeftPart(UriPartial.Authority));
        await client.ConnectAsync(ws,deadline.Token).ConfigureAwait(false);
        await Send("M1,1,0.25,-0.4,2,0,0,0,3","10").ConfigureAwait(false);
        Assert(bridge.Read() is {Ready:true,HasMotion:true,Left:{X:.25,Y:-.4,Stomps:2}},"Bridge discarded the calibrated motion");
        await Send("M1,?,0,0,2,0,0,0,3","?0").ConfigureAwait(false);
        Assert(bridge.Read() is {Ready:false},"Unknown state was treated as off-board");
        await Task.Delay(950,deadline.Token).ConfigureAwait(false);
        Assert(bridge.Read()==null,"Stale data still reported as live");

        async Task Reject(Uri target,string origin)
        {
            using var other=new ClientWebSocket();
            other.Options.SetRequestHeader("Origin",origin);
            try { await other.ConnectAsync(target,deadline.Token).ConfigureAwait(false); }
            catch(WebSocketException) { return; }
            throw new Exception("Bridge accepted an unauthorized client");
        }
        async Task Send(string message,string expected)
        {
            await client.SendAsync(Encoding.ASCII.GetBytes(message).AsMemory(),WebSocketMessageType.Text,true,deadline.Token).ConfigureAwait(false);
            var bytes=new byte[3];
            var result=await client.ReceiveAsync(bytes.AsMemory(),deadline.Token).ConfigureAwait(false);
            Assert(result.MessageType==WebSocketMessageType.Text&&result.EndOfMessage&&Encoding.ASCII.GetString(bytes,0,result.Count)==expected,
                "Bridge did not acknowledge the applied contacts");
        }
    }
}
