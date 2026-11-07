using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MNN.Unity;
using MNN.Unity.Interop;
class AdvancedSmoke
{
 static int checks;
 static void Check(bool condition,string name) {if(!condition)throw new Exception(name); Console.WriteLine("PASS "+name);checks++;}
 static void Equal(float[] expected,float[] actual,string name)
 {
  Check(expected.Length==actual.Length,name+" length");
  for(int i=0;i<actual.Length;i++)if(Math.Abs(expected[i]-actual[i])>1e-5)throw new Exception(name+" at "+i+" actual="+actual[i]);
  Console.WriteLine("VALUES "+name+" count="+actual.Length+"=["+string.Join(",",actual.Take(8))+(actual.Length>8?",...":"")+"]");checks++;
 }
 static void Express()
 {
  byte[] graph=File.ReadAllBytes("affine.mnn");
  foreach(int threads in new[]{1,4})
  using(var e=new MNNInterop.ExpressExecutor(threads,MNNBackendType.CPU))
  using(e.Enter())
  {
   Check(e.Backend==MNNBackendType.CPU,"Express CPU threads="+threads);
   var values=new[]{-1f,0f,.5f,5f};
   var pin=GCHandle.Alloc(values,GCHandleType.Pinned);
   var variable=default(MNNInterop.CppShared);
   try
   {
    variable=MNNInterop.Constant(pin.AddrOfPinnedObject(),new[]{1,4},2,new MNNInterop.HalideType{Code=2,Bits=32,Lanes=1});
    GC.Collect();GC.WaitForPendingFinalizers();
    Equal(values,MNNInterop.ReadFloat(variable,out var shape),"constant roundtrip");
    Check(shape.SequenceEqual(new[]{1,4}),"constant shape");
    Equal(new[]{-1f,1f,2f,11f},MNNInterop.RunSingleInputModule(graph,"input","output",variable),"Module affine");
   }
   finally{if(variable.Object!=IntPtr.Zero)MNNInterop.DestroyVariable(ref variable);pin.Free();}
  }
  for(int round=0;round<3;round++)
  using(var g=new MNNExpressGraph("affine.mnn",1,MNNBackendType.CPU,preparedGraph:graph))
  {
   Check(g.ContainsVariable("input")&&g.ContainsVariable("output"),"loaded named variables round="+round);
   GC.Collect();GC.WaitForPendingFinalizers();
   var output=g.RunOutputs(new[]{"output"},new[]{new MNNGenerationGraph.Input("input",new[]{-1f,0f,.5f,5f},1,4)})[0];
   Equal(new[]{-1f,1f,2f,11f},output.Values,"Variable affine");
   Check(output.Shape.SequenceEqual(new[]{1,4}),"Variable shape");
   var resized=g.RunOutputs(new[]{"output"},new[]{new MNNGenerationGraph.Input("input",new[]{-1f,2f,3.25f,4f,0f,100f},1,6)})[0];
   Equal(new[]{-1f,5f,7.5f,9f,1f,201f},resized.Values,"Variable resized affine");
   Check(resized.Shape.SequenceEqual(new[]{1,6}),"Variable resized shape");
   g.Dispose();bool disposed=false;try{g.GetVariableNames();}catch(ObjectDisposedException){disposed=true;}Check(disposed,"Express disposed rejected");
  }
  Console.WriteLine("EXPRESS_ADVANCED_PASSED checks="+checks);
 }
 static void Llm(string directory)
 {
  Directory.CreateDirectory("cache");
  using(var h=MNNLlm.Load(directory,1,cacheDirectory:Path.GetFullPath("cache"),precisionMode:MNNPrecisionMode.High,backendType:MNNBackendType.CPU))
  {
   string config=h.DumpConfiguration();Check(config.Contains("backend_type"),"owned config string");
   Check(h.Capabilities==MNNModelCapabilities.Text,"text capabilities");
   int tokens=h.CountTokens("Hello 世界 😀");Check(tokens>1,"UTF8 tokenization count="+tokens);
   for(int i=0;i<20;i++){Check(h.CountTokens("x"+i)>0,"owned token vector distinct input "+i);GC.Collect();}
   var first=h.Generate("What is the capital of France?",8);Check(first.GeneratedTokens>0&&first.PromptTokens>0&&!string.IsNullOrEmpty(first.Text),"text generation"); Console.WriteLine("GENERATED="+first.Text.Replace("\n","\\n"));
   var second=h.Generate("What is the capital of France?",8);Check(second.Text==first.Text,"reset independent greedy repeat");
   var messages=new[]{new MNNChatMessage(MNNChatRole.User,"What is the capital of France?")};
   Check(h.CountConversationTokens(messages)>tokens,"owned chat template count");
   string cumulative="";int updates=0;
   var stream=h.GenerateConversationStreaming(messages,u=>{updates++;cumulative=u.Text;GC.Collect();},8);
   Check(updates>0&&stream.GeneratedTokens>0&&cumulative==stream.Text,"stream cumulative equals result");
   Console.WriteLine("CHAT="+stream.Text.Replace("\n","\\n"));
   var parts=new StringBuilder();
   var deltas=h.GenerateConversationStreamingDeltas(messages,d=>{parts.Append(d.Text);GC.Collect();},8);
   Check(parts.ToString()==deltas.Text&&deltas.Text==stream.Text,"stream deltas equals deterministic result");
   Check(deltas.ConversationCacheStatus==MNNConversationCacheStatus.UnsupportedRuntime,"x64 cache uses verified full prefill");
   using(var c=new CancellationTokenSource())
   {
    var cancelled=h.GenerateConversationStreaming(messages,u=>c.Cancel(),8,cancellationToken:c.Token);
    Check(cancelled.Cancelled&&cancelled.GeneratedTokens>0,"stream cancellation observed between tokens");
   }
   var after=h.GenerateConversation(messages,8);
   Check(after.Text==stream.Text,"generation recovers after cancellation");
   h.Dispose();h.Dispose();bool disposed=false;try{h.CountTokens("x");}catch(ObjectDisposedException){disposed=true;}Check(disposed,"LLM disposed rejects calls");
  }
  GC.Collect();GC.WaitForPendingFinalizers();
  Console.WriteLine("LLM_ADVANCED_PASSED checks="+checks);
 }
 static void Embedding(string directory)
 {
  Environment.SetEnvironmentVariable("MNN_INFERENCE_CACHE_DIRECTORY",Path.GetFullPath("cache"));
  using(var model=MNNEmbedding.Load(directory,1,cacheDirectory:Path.GetFullPath("cache"),precisionMode:MNNPrecisionMode.High,backendType:MNNBackendType.CPU))
  {
   var a=model.Encode("中国的首都是哪里？");
   Check(a.Length==model.Dimension&&a.Length>0,"embedding dimension="+a.Length);
   double norm=Math.Sqrt(a.Sum(x=>(double)x*x));Check(Math.Abs(norm-1)<.002,"embedding finite unit norm="+norm);
   GC.Collect();GC.WaitForPendingFinalizers();
   var b=model.Encode("北京是中国的首都。");var c=model.Encode("香蕉是一种水果。");
   double good=a.Zip(b,(x,y)=>(double)x*y).Sum(),bad=a.Zip(c,(x,y)=>(double)x*y).Sum();Console.WriteLine("COSINE relevant="+good+" unrelated="+bad);Check(good>bad,"embedding retrieval order");
   Equal(b,model.Encode("北京是中国的首都。"),"embedding independent repeated text");
   model.Dispose();bool disposed=false;try{model.Encode("x");}catch(ObjectDisposedException){disposed=true;}Check(disposed,"embedding disposed rejected");
  }
  Console.WriteLine("EMBEDDING_ADVANCED_PASSED checks="+checks);
 }
 static void Reranker(string directory)
 {
  using(var model=MNNReranker.Load(directory,1,cacheDirectory:Path.GetFullPath("cache"),precisionMode:MNNPrecisionMode.High,backendType:MNNBackendType.CPU))
  {
   float relevant=model.Score("What is France's capital?","Paris is the capital of France.");
   GC.Collect();GC.WaitForPendingFinalizers();
   float unrelated=model.Score("What is France's capital?","Bananas are fruit.");
   Console.WriteLine("SCORE relevant="+relevant+" unrelated="+unrelated);Check(relevant>unrelated,"reranker relevance order");
   Check(relevant>=0&&relevant<=1&&unrelated>=0&&unrelated<=1,"reranker finite probabilities");
   model.Dispose();bool disposed=false;try{model.Score("x","y");}catch(ObjectDisposedException){disposed=true;}Check(disposed,"reranker disposed rejected");
  }
  Console.WriteLine("RERANKER_ADVANCED_PASSED checks="+checks);
 }
 static void Omni(string directory)
 {
  File.WriteAllBytes("red.png",Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAb0lEQVR4nO3PAQkAAAyEwO9feoshgnABdLep8QUNyPEFDcjxBQ3I8QUNyPEFDcjxBQ3I8QUNyPEFDcjxBQ3I8QUNyPEFDcjxBQ3I8QUNyPEFDcjxBQ3I8QUNyPEFDcjxBQ3I8QUNyPEFDcjxBQ3IPanc8OLDQitxAAAAAElFTkSuQmCC"));
  using(var model=MNNLlm.Load(directory,1,cacheDirectory:Path.GetFullPath("cache"),precisionMode:MNNPrecisionMode.High,backendType:MNNBackendType.CPU))
  {
   Check((model.Capabilities&MNNModelCapabilities.Vision)!=0,"Omni visual capability");
   var result=model.GenerateMultimodal("What color is the image? Answer one word.",imagePath:Path.GetFullPath("red.png"),maxNewTokens:4);
   Console.WriteLine("VISION_TEXT="+result.Text.Replace("\n","\\n")+" vision_us="+result.VisionMicroseconds);
   Check(result.VisionMicroseconds>0&&result.GeneratedTokens>0,"Omni processed image and generated text");
  }
  Console.WriteLine("OMNI_ADVANCED_PASSED checks="+checks);
 }
 static int Main(string[] args)
 {
  try{Console.WriteLine("MODE="+args[0]+" Platform="+UnityEngine.Application.platform);if(args[0]=="express")Express();else if(args[0]=="embedding")Embedding(args[1]);else if(args[0]=="reranker")Reranker(args[1]);else if(args[0]=="omni")Omni(args[1]);else Llm(args[1]);return 0;}
  catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
