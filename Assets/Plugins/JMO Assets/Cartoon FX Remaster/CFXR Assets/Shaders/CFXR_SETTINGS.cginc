//--------------------------------------------------------------------------------------------------------------------------------
// Cartoon FX
// (c) 2012-2025 Jean Moreno
//--------------------------------------------------------------------------------------------------------------------------------

// Global settings for the Cartoon FX Remaster shaders

//--------------------------------------------------------------------------------------------------------------------------------


/* Uncomment this line if you want to globally disable soft particles */
/* Enabled for this project: the Mobile URP asset has no camera depth texture, so soft
   particles would fade every CFXR effect to fully transparent. */
#define GLOBAL_DISABLE_SOFT_PARTICLES

/* Change this value if you want to globally scale the HDR effects */
/* (e.g. if your bloom effect is too strong or too weak on the effects) */
#define GLOBAL_HDR_MULTIPLIER 1

/* Comment this line if you want to disable point lights for lit particles */
#define ENABLE_POINT_LIGHTS


//--------------------------------------------------------------------------------------------------------------------------------