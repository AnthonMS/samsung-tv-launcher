using System;

public static class HomeFeedLayout
{
 public static int Reveal(int scroll,int top,int bottom,int viewport,int content,int margin=8){
  int max=Math.Max(0,content-viewport);scroll=Math.Clamp(scroll,0,max);
  if(top<scroll+margin)scroll=top-margin;
  else if(bottom>scroll+viewport-margin)scroll=bottom+margin-viewport;
  return Math.Clamp(scroll,0,max);
 }
}
