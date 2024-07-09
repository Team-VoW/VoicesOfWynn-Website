#!/bin/bash


# Update website DBInfo.ini
sed -i "s/host=.*/host=$WEBSITE_DB_HOST/" /var/www/html/Models/website/DBInfo.ini
sed -i "s/database=.*/database=$WEBSITE_DB_NAME/" /var/www/html/Models/website/DBInfo.ini
sed -i "s/username=.*/username=$WEBSITE_DB_USER/" /var/www/html/Models/website/DBInfo.ini
sed -i "s/password=.*/password=$WEBSITE_DB_PASSWORD/" /var/www/html/Models/website/DBInfo.ini

# Update API DBInfo.ini files
for file in /var/www/html/Models/Api/*/DBInfo.ini; do
    sed -i "s/host=.*/host=$API_DB_HOST/" "$file"
    sed -i "s/database=.*/database=$API_DB_NAME/" "$file"
    sed -i "s/username=.*/username=$API_DB_USER/" "$file"
    sed -i "s/password=.*/password=$API_DB_PASSWORD/" "$file"
done

sleep 10

# Change to the liquibase directory
cd /var/www/html/liquibase

# Run Liquibase for website database
liquibase \
    --changelog-file=db.changelog-master-website.yml \
    --url=jdbc:mysql://$WEBSITE_DB_HOST:3306/$WEBSITE_DB_NAME \
    --username=$WEBSITE_DB_USER \
    --password=$WEBSITE_DB_PASSWORD \
    --driver=com.mysql.cj.jdbc.Driver \
    update

# Run Liquibase for API database
liquibase \
    --changelog-file=db.changelog-master-api.yml \
    --url=jdbc:mysql://$API_DB_HOST:3306/$API_DB_NAME \
    --username=$API_DB_USER \
    --password=$API_DB_PASSWORD \
    --driver=com.mysql.cj.jdbc.Driver \
    update

# Change back to the original directory
cd /var/www/html

# Start Apache in foreground
apache2-foreground