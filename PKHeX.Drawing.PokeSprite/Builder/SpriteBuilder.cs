using System;
using System.Drawing;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite.Properties;

namespace PKHeX.Drawing.PokeSprite;

public abstract class SpriteBuilder : ISpriteBuilder<Image>
{
    public static bool ShowEggSpriteAsItem { get; set; } = true;
    public static bool ShowEncounterBall { get; set; } = true;
    public static SpriteBackgroundType ShowEncounterColor { get; set; } = SpriteBackgroundType.FullBackground;
    public static SpriteBackgroundType ShowEncounterColorPKM { get; set; }
    public static SpriteBackgroundType ShowTeraType { get; set; } = SpriteBackgroundType.TopStripe;
    public static bool ShowExperiencePercent { get; set; }
    public static byte ShowTeraOpacityStripe { get; set; }
    public static int ShowTeraThicknessStripe { get; set; }
    public static byte ShowTeraOpacityBackground { get; set; }
    public static byte ShowEncounterOpacityStripe { get; set; }
    public static byte ShowEncounterOpacityBackground { get; set; }
    public static int ShowEncounterThicknessStripe { get; set; }

    /// <summary> Width of the generated Sprite image. </summary>
    public abstract int Width { get; }
    /// <summary> Height of the generated Sprite image. </summary>
    public abstract int Height { get; }

    /// <summary> Minimum amount of padding on the right side of the image when layering an item sprite. </summary>
    protected abstract int ItemShiftX { get; }
    /// <summary> Minimum amount of padding on the bottom side of the image when layering an item sprite. </summary>
    protected abstract int ItemShiftY { get; }
    /// <summary> Max width / height of an item image. </summary>
    protected abstract int ItemMaxSize { get; }

    protected abstract int EggItemShiftX { get; }
    protected abstract int EggItemShiftY { get; }

    public abstract bool HasFallbackMethod { get; }

    public abstract Bitmap Hover { get; }
    public abstract Bitmap View { get; }
    public abstract Bitmap Set { get; }
    public abstract Bitmap Delete { get; }
    public abstract Bitmap Transparent { get; }
    public abstract Bitmap Drag { get; }
    public abstract Bitmap UnknownItem { get; }
    public abstract Bitmap None { get; }
    public abstract Bitmap ItemTM { get; }
    public abstract Bitmap ItemTR { get; }

    private const double UnknownFormTransparency = 0.5;
    private const double ShinyTransparency = 0.7;
    private const double EggUnderLayerTransparency = 0.33;

    protected abstract string GetSpriteStringSpeciesOnly(ushort species);

    protected abstract string GetSpriteAll(ushort species, byte form, int gender, uint formarg, bool shiny, EntityContext context);
    private static string GetLumiSprite(ushort species, byte form, int gender, uint formarg, bool shiny) => 'b' + SpriteName.GetLumiResourceStringSprite(species, form, gender, formarg, shiny);
    protected abstract string GetSpriteAllSecondary(ushort species, byte form, int gender, uint formarg, bool shiny, EntityContext context);
    protected abstract string GetItemResourceName(int item);
    protected abstract Bitmap Unknown { get; }
    protected abstract Bitmap GetEggSprite(ushort species);
    public abstract Bitmap ShadowLugia { get; }

    /// <summary>
    /// Ensures all data is set up to generate sprites for the save file.
    /// </summary>
    /// <param name="sav"></param>
    public void Initialize(SaveFile sav)
    {
        if (sav.Generation != 3 && sav is not SAV8BSLuminescent)
            return;

        // If the game is indeterminate, we might have different form sprites.
        // Currently, this only applies to Gen3's FireRed / LeafGreen
        Game = sav.Version;
        if (Game == GameVersion.FRLG)
            Game = ReferenceEquals(sav.Personal, PersonalTable.FR) ? GameVersion.FR : GameVersion.LG;
        if (sav is SAV8BSLuminescent)
            Game = GameVersion.BDSPLUMI;
    }

    private GameVersion Game;

    private static byte GetDeoxysForm(GameVersion game) => game switch
    {
        GameVersion.FR => 1, // Attack
        GameVersion.LG => 2, // Defense
        GameVersion.E => 3, // Speed
        _ => 0,
    };

    private static byte GetArceusForm4(byte form) => form switch
    {
        > 9 => --form, // Realign to Gen5+ type indexes
        9 => byte.MaxValue, // Curse, make it show as unrecognized form since we don't have a sprite.
        _ => form,
    };

    private static bool IsNotInLumi(ushort species, byte form)
    {
        foreach (ushort naspecies in FormInfo.WithUnavailableForm)
        {
            if (species == naspecies)
            {
                var forms = FormInfo.Unavailable(species);

                if (forms == Array.Empty<byte>()) return false;

                for (int i = 0; i < forms.Length; i++)
                {
                    if (form != forms[i]) continue;
                    return true;
                }
            }
        };

        foreach (ushort inspecies in FormInfo.NewGenAvailables)
        {
            if (species == inspecies)
                return false;
        };

        if (species < 494) return false;
        else return true;
    }

    private static bool IsLumiForm(ushort species, byte form) => (Species)species switch
    {
        Species.Venusaur or Species.Blastoise or Species.Gengar or Species.Eevee when form == 3 => true,
        Species.Charizard when form == 4 => true,
        Species.Pikachu when form == 17 => true,
        Species.Onix when form == 1 => true,
        Species.Mewtwo when form == 3 || form == 4 => true,
        _ => false
    };

    /// <summary>
    /// Builds a new sprite image with the requested parameters.
    /// </summary>
    /// <param name="species">Entity Species ID</param>
    /// <param name="form">Entity Form index</param>
    /// <param name="gender">Entity gender</param>
    /// <param name="formarg">Entity <see cref="IFormArgument.FormArgument"/> raw value</param>
    /// <param name="heldItem">Entity held item ID</param>
    /// <param name="isEgg">Is currently in an egg</param>
    /// <param name="shiny">Is it shiny</param>
    /// <param name="context">Context the sprite is for</param>
    public Image GetSprite(ushort species, byte form, int gender, uint formarg, int heldItem, bool isEgg, Shiny shiny = Shiny.Never, EntityContext context = EntityContext.None)
    {
        if (species == 0)
            return None;

        if (context == EntityContext.Gen3 && species == (int)Species.Deoxys) // Depends on Gen3 save file version
            form = GetDeoxysForm(Game);
        else if (context == EntityContext.Gen4 && species == (int)Species.Arceus) // Curse type's existence in Gen4
            form = GetArceusForm4(form);

        var baseImage = GetBaseImage(species, form, gender, formarg, shiny.IsShiny(), context);
        return GetSprite(baseImage, species, heldItem, isEgg, shiny, context);
    }

    public Image GetSprite(Image baseSprite, ushort species, int heldItem, bool isEgg, Shiny shiny, EntityContext context = EntityContext.None)
    {
        if (isEgg)
            baseSprite = LayerOverImageEgg(baseSprite, species, heldItem != 0);
        if (heldItem > 0)
            baseSprite = LayerOverImageItem(baseSprite, heldItem, context);
        if (shiny.IsShiny())
        {
            if (shiny == Shiny.AlwaysSquare && context.Generation() != 8)
                shiny = Shiny.Always;
            baseSprite = LayerOverImageShiny(baseSprite, shiny);
        }
        return baseSprite;
    }

    private Image GetBaseImage(ushort species, byte form, int gender, uint formarg, bool shiny, EntityContext context)
    {
        var img = FormInfo.IsTotemForm(species, form, context)
            ? GetBaseImageTotem(species, form, gender, formarg, shiny, context)
            : GetBaseImageDefault(species, form, gender, formarg, shiny, context);
        return img ?? GetBaseImageFallback(species, form, gender, formarg, shiny, context);
    }

    private Image? GetBaseImageTotem(ushort species, byte form, int gender, uint formarg, bool shiny, EntityContext context)
    {
        var baseform = FormInfo.GetTotemBaseForm(species, form);
        var baseImage = GetBaseImageDefault(species, baseform, gender, formarg, shiny, context);
        if (baseImage is not Bitmap b)
            return null;

        SpriteUtil.GetSpriteGlow(baseImage, 0, 165, 255, out var pixels, true);
        var layer = ImageUtil.GetBitmap(pixels, b.Width, b.Height, b.PixelFormat);
        return ImageUtil.LayerImage(baseImage, layer, 0, 0);
    }

    private Image? GetBaseImageDefault(ushort species, byte form, int gender, uint formarg, bool shiny, EntityContext context)
    {
        var lumi = Game is GameVersion.BDSPLUMI;
        var turnDitto = IsNotInLumi(species, form);
        var lumiForm = IsLumiForm(species, form);

        var file = lumi ? lumiForm ? GetLumiSprite(species, form, gender, formarg, shiny)
            : turnDitto ? GetSpriteAll((ushort)Species.Ditto, 0, 0, 0, false, context)
            : GetSpriteAll(species, form, gender, formarg, shiny, context)
            : GetSpriteAll(species, form, gender, formarg, shiny, context);

        var resource = (Image?)Resources.ResourceManager.GetObject(file);
        if (resource is null && HasFallbackMethod)
        {
            file = GetSpriteAllSecondary(species, form, gender, formarg, shiny, context);
            resource = (Image?)Resources.ResourceManager.GetObject(file);
        }
        return resource;
    }

    private Image GetBaseImageFallback(ushort species, byte form, int gender, uint formarg, bool shiny, EntityContext context)
    {
        if (shiny) // try again without shiny
        {
            var img = GetBaseImageDefault(species, form, gender, formarg, false, context);
            if (img != null)
                return img;
        }

        // try again without form
        var baseImage = (Image?)Resources.ResourceManager.GetObject(GetSpriteStringSpeciesOnly(species));
        if (baseImage == null) // failed again
            return Unknown;
        return ImageUtil.LayerImage(baseImage, Unknown, 0, 0, UnknownFormTransparency);
    }

    private Image LayerOverImageItem(Image baseImage, int item, EntityContext context)
    {
        var lump = HeldItemLumpUtil.GetIsLump(item, context);
        var itemimg = lump switch
        {
            HeldItemLumpImage.TechnicalMachine => ItemTM,
            HeldItemLumpImage.TechnicalRecord => ItemTR,
            _ => (Image?)Resources.ResourceManager.GetObject(GetItemResourceName(item)) ?? UnknownItem,
        };

        // Redraw item in bottom right corner; since images are cropped, try to not have them at the edge
        int x = baseImage.Width - itemimg.Width - ((ItemMaxSize - itemimg.Width) / 4) - ItemShiftX;
        int y = baseImage.Height - itemimg.Height - ItemShiftY;
        return ImageUtil.LayerImage(baseImage, itemimg, x, y);
    }

    private static Image LayerOverImageShiny(Image baseImage, Shiny shiny)
    {
        // Add shiny star to top left of image.
        Bitmap rare;
        if (shiny is Shiny.AlwaysSquare)
            rare = Resources.rare_icon_alt_2;
        else
            rare = Resources.rare_icon_alt;
        return ImageUtil.LayerImage(baseImage, rare, 0, 0, ShinyTransparency);
    }

    private Image LayerOverImageEgg(Image baseImage, ushort species, bool hasItem)
    {
        if (ShowEggSpriteAsItem && !hasItem)
            return LayerOverImageEggAsItem(baseImage, species);
        return LayerOverImageEggTransparentSpecies(baseImage, species);
    }

    private Image LayerOverImageEggTransparentSpecies(Image baseImage, ushort species)
    {
        // Partially transparent species.
        baseImage = ImageUtil.ChangeOpacity(baseImage, EggUnderLayerTransparency);
        // Add the egg layer over-top with full opacity.
        var egg = GetEggSprite(species);
        return ImageUtil.LayerImage(baseImage, egg, 0, 0);
    }

    private Image LayerOverImageEggAsItem(Image baseImage, ushort species)
    {
        var egg = GetEggSprite(species);
        return ImageUtil.LayerImage(baseImage, egg, EggItemShiftX, EggItemShiftY); // similar to held item, since they can't have any
    }

    public static void LoadSettings(ISpriteSettings sprite)
    {
        ShowEggSpriteAsItem = sprite.ShowEggSpriteAsHeldItem;
        ShowEncounterBall = sprite.ShowEncounterBall;

        ShowEncounterColor = sprite.ShowEncounterColor;
        ShowEncounterColorPKM = sprite.ShowEncounterColorPKM;
        ShowEncounterThicknessStripe = sprite.ShowEncounterThicknessStripe;
        ShowEncounterOpacityBackground = sprite.ShowEncounterOpacityBackground;
        ShowEncounterOpacityStripe = sprite.ShowEncounterOpacityStripe;
        ShowExperiencePercent = sprite.ShowExperiencePercent;

        ShowTeraType = sprite.ShowTeraType;
        ShowTeraThicknessStripe   = sprite.ShowTeraThicknessStripe;
        ShowTeraOpacityBackground = sprite.ShowTeraOpacityBackground;
        ShowTeraOpacityStripe     = sprite.ShowTeraOpacityStripe;
    }
}
