# Step 1: Build the application
FROM oven/bun AS builder

# Set the working directory in the container
WORKDIR /app

# Copy all the application files to the container
COPY . .

# Run your build process
RUN bun i
ENV NODE_ENV=production
RUN bun run build


# Expose the port the application will run on
EXPOSE 5173

#Start the BUN server
CMD ["bun", "run", "--bun", "dev", "--host"]