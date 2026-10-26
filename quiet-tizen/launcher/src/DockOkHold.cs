// One physical OK press can either open an app or enter customization, never both.
public sealed class DockOkHold
{
 public bool Pending {get;private set;}
 public bool Consumed {get;private set;}
 public bool Active=>Pending||Consumed;
 string appId;
 public bool Press(string id){if(Active)return false;appId=id;Pending=true;return true;}
 public string Hold(){if(!Pending)return null;Pending=false;Consumed=true;return appId;}
 public string Release(){string open=Pending?appId:null;Cancel();return open;}
 public void Cancel(){Pending=false;Consumed=false;appId=null;}
}
