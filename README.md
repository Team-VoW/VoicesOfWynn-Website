# Voices Of Wynn Website

This repository stores code for the public website presenting the community project **Voices of Wynn** – a mod for Minecraft that allows all players listen to NPC dialogue on the MMORPG server Wynncraft instead of reading it.

## For Developerts

### Running the website locally

To run the website locally you need to make sure you have your Docker engine with docker-compose running. An easy way of doing this is downloading [Rancher Desktop](https://rancherdesktop.io/) and simply running the application. Once you have that simply run:

```bash
docker-compose -f docker-compose.dev.yml up
```

to force it to build the web container you can add the `--build` flag:

```bash
docker-compose -f docker-compose.dev.yml up --build
```

after having run it once you do not need to include the --build in future startups which will make starting it faster

this will create all the containers (databases and everything) for you.

Once the containers are running, you can access the following services:

- **Website:** [http://localhost](http://localhost) or [http://127.0.0.1](http://127.0.0.1)  
  The main Voices of Wynn website will be available here.

- **phpMyAdmin:** [http://localhost:8080](http://localhost:8080)  
  Use this interface to manage and inspect the MySQL database.

- **API Documentation (Swagger UI):** [http://localhost:8090](http://localhost:8090)  
  Explore and test the API endpoints using Swagger UI.

If you have some weird issues or want to reset everything do the command:

```bash
docker system prune -a --volumes
```

after which you will have to run the docker-compose command again with --build.

### Liquibase

We use liquibase as a database schema change management tool. It allows you to manage and track database schema changes in a version-controlled manner, making it easier to deploy and maintain database changes across different environments.

To change anything about the database structure you need to create a new changeset and add it at the bottom of the changelog found in the `liquibase` directory. The changset name should get a title such as:
`-- changeset <name>:<yourChangesetNum>` so if your name is kmaxi and this is the first change YOU are making it should look like this:
`-- changeset kmaxi:1`.

For more information on how to use Liquibase, refer to the [official documentation](https://www.liquibase.org/documentation/index.html).

## Planned featuers

🔲 Index page with basic information about the project  
🔲 Downloads page with clear download links  
🔲 Contributors page with list of all contributors and detailed information about their contributions  
🔲 Recordings page with all recordings  
🔲 Upvote/downvote system for all recordings  
🔲 Login system for all contributors, allowing them to change their displayed profile picture, name and other information  
🔳 Suggest other features in Issues
