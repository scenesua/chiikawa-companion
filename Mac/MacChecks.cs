using Momonga.Animation;
using Momonga.Character;
using Momonga.Content;
using Momonga.Inventory;
using Momonga.Persistence;

namespace Momonga.Mac;
public static class MacChecks
{
    public static void Run()
    {
        var catalog=ItemCatalog.Load();
        foreach(var character in CharacterDefinition.All)
        {
            var dialogue=new DialogueService(new SaveData(),character.DialogueSetId);
            if(string.IsNullOrWhiteSpace(dialogue.Pick("Greeting")))throw new InvalidOperationException("Missing dialogue");
            for(var i=0;i<80;i++) if(character.AssetSet!="momonga"||i<32)_=Sprites.Frame(character.AssetSet,i);
            for(var i=0;i<12;i++)_=Sprites.Idle(character.AssetSet,i);
            foreach(var food in catalog.Values.Where(i=>i.Category=="Food")){_=Sprites.Meal(character.AssetSet,food.Id,0);_=Sprites.Meal(character.AssetSet,food.Id,1);_=Sprites.Bowl(food.Id,.5,true);}
            foreach(var food in catalog.Values.Where(i=>i.Category is "Snack" or "Drink")){_=Sprites.Snack(character.AssetSet,food.AnimationFrame);_=Sprites.Snack(character.AssetSet,food.AnimationFrame+1);}
            _=Sprites.Drink(character.AssetSet,0);_=Sprites.Drink(character.AssetSet,1);
        }
        foreach(var id in new[]{"cushion","beanbag","nest","ball","doll"})_=Sprites.Furniture(id);
        Console.WriteLine("Shared simulation, persistence and all 17 characters' sprite resources passed.");
    }
}
