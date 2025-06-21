import matplotlib.pyplot as plt
import pandas as pd
import os

# Get CSVs from game directory
profiles = {}
for filename in os.listdir('../Pestis'):
    if filename.endswith('.csv'):
        with open(os.path.join('../Pestis', filename), 'r') as file:
            df = pd.read_csv(file)
            profiles[filename.split("-")[0]] = df

for profile, df in profiles.items():
    # Average iterations

    new = pd.DataFrame()
    new["Average"] = df.groupby("TotalRats")["FPS"].mean()

    plot = new.plot(title=profile)
    plt.xlabel("Rats")
    plt.ylabel("FPS")
    ax = plt.gca()
    ax.set_ylim((0, 144))
    plt.show()