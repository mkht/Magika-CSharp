namespace magika;

internal static class Assert{
    public static void IsTrue(bool condition, string message = ""){
        if(!condition){
            throw new System.Exception(message);
        }
    }
}
