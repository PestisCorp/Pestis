from PIL import Image
from itertools import combinations, product
import os

baseTopImages = []
baseDownImages = []
baseDownLeftImages = []
baseLeftImages = []
baseUpLeftImages = []

featuresTopImages = {}
featuresDownImages = {}
featuresDownLeftImages = {}
featuresLeftImages = {}
featuresUpLeftImages = {}

baseDir = os.fsencode("Base")
features = ["Bandage", "Components", "hats", "Pickles"]

# Load all features and put into respective direction lists
for feature in features:
    featureDir = os.fsencode(feature)
    for featureTypeDir in os.listdir(featureDir):
        subtypeName = feature + "/" + os.fsdecode(featureTypeDir)
        for subtype in os.listdir(os.fsencode(subtypeName)):
            featureName = os.fsdecode(subtype)
            path = subtypeName + "/" + featureName
            if "top" in featureName.lower():
                if feature in featuresTopImages:
                    featuresTopImages[feature].append(Image.open(path))
                else:
                    featuresTopImages[feature] = [Image.open(path)]
                continue

            if "left down" in featureName.replace("_", " ").lower():
                if feature in featuresDownLeftImages:
                    featuresDownLeftImages[feature].append(Image.open(path))
                else:
                    featuresDownLeftImages[feature] = [Image.open(path)]
                continue

            if "up left" in featureName.replace("_", " ").lower():
                if feature in featuresUpLeftImages:
                    featuresUpLeftImages[feature].append(Image.open(path))
                else:
                    featuresUpLeftImages[feature] = [Image.open(path)]
                continue

            if "down" in featureName.lower():
                if feature in featuresDownImages:
                    featuresDownImages[feature].append(Image.open(path))
                else:
                    featuresDownImages[feature] = [Image.open(path)]
                continue

            if "left" in featureName.lower():
                if feature in featuresLeftImages:
                    featuresLeftImages[feature].append(Image.open(path))
                else:
                    featuresLeftImages[feature] = [Image.open(path)]
                continue

# Load all the base rats and put into direction lists
for baseTypeDir in os.listdir(baseDir):
    typeName = "Base" + "/" + os.fsdecode(baseTypeDir)
    for base in os.listdir(os.fsencode(typeName)):
        baseName = os.fsdecode(base)
        path = typeName + "/" + baseName
        if "top" in baseName.lower():
            baseTopImages.append(Image.open(path))
            continue
        if "left down" in baseName.replace("_", " ").lower():
            baseDownLeftImages.append(Image.open(path))
            continue
        if "up left" in baseName.replace("_", " ").lower():
            baseUpLeftImages.append(Image.open(path))
            continue
        if "down" in baseName.lower():
            baseDownImages.append(Image.open(path))
            continue
        if "left" in baseName.lower():
            baseLeftImages.append(Image.open(path))
            continue

def generate_combinations(baseList, featureDict, direction):
    j = 0
    # Generate all the combinations of rats
    for i in range(0, len(featureDict) + 1):
        # To get all combinations, you want to have the base either by itself, or combined
        # with up to len(features) + 1 other components
        for type_combination in combinations(featureDict, i):
            for variant_combination in product(*(featureDict[t] for t in type_combination)):
                for base in baseList:
                    combined = base.copy()
                    for component in variant_combination:
                        combined.paste(component, (0,0), component)
                    combined.save(f"out/{direction}/rat_{direction}_{j}.png", "png")
                    j += 1

# Now generate for every direction
directions = ["Top", "DownLeft", "Left", "UpLeft", "Down"]
for direction in directions:
    baseList = globals()[f"base{direction}Images"]
    featuresDict = globals()[f"features{direction}Images"]
    generate_combinations(baseList, featuresDict, direction)
