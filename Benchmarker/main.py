import matplotlib.pyplot as plt
import pandas as pd
import os
import json

# Get CSVs from game directory
profiles = {}
for filename in os.listdir('../Pestis'):
    if filename.endswith('.csv'):
        with open(os.path.join('../Pestis', filename), 'r') as file:
            df = pd.read_csv(file)

            profiles[filename.split("-")[1]] = (df, filename)

latest_profile = max(profiles.keys())

profile_name = input("Enter profile name: ")
os.makedirs(profile_name, exist_ok=True)

profile = profiles[latest_profile][0]
os.rename(f"../Pestis/{profiles[latest_profile][1]}", f"{profile_name}/data.csv")

new = pd.DataFrame()
new["Average"] = df.groupby("TotalRats")["FPS"].mean()

plot = new.plot(title=profile_name)
plt.xlabel("Rats")
plt.ylabel("FPS")
ax = plt.gca()
ax.set_ylim((0, 144))
plt.savefig(f"{profile_name}/chart.png")

# Crop FPS below 144
df = df[df["FPS"].between(0, 144)]

data = {
    "fps": {
        "mean": df["FPS"].mean(),
        "median": df["FPS"].median()
    }
}

json.dump(data, open(f"{profile_name}/data.json", "w+"))