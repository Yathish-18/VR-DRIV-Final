Shader "Hidden/PampelGames/Construction/InvisibleShader"  
{  
    SubShader  
    {  
        Pass  
        {  
            ZWrite Off  
            Blend SrcAlpha OneMinusSrcAlpha            ColorMask 0  
        }  
    }
}